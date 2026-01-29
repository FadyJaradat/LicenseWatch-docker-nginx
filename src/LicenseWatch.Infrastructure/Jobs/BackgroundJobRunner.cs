using LicenseWatch.Core.Diagnostics;
using LicenseWatch.Core.Entities;
using LicenseWatch.Core.Jobs;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Compliance;
using LicenseWatch.Infrastructure.Email;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Infrastructure.Reports;
using LicenseWatch.Infrastructure.Usage;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Infrastructure.Jobs;

public class BackgroundJobRunner
{
    private const string SystemActor = "system";

    private readonly AppDbContext _dbContext;
    private readonly IAuditLogger _auditLogger;
    private readonly IUsageAggregator _usageAggregator;
    private readonly IComplianceEvaluator _complianceEvaluator;
    private readonly IEmailNotificationService _notificationService;
    private readonly IReportDeliveryService _reportDeliveryService;
    private readonly IBootstrapSettingsStore _settingsStore;
    private readonly ILogger<BackgroundJobRunner> _logger;

    public BackgroundJobRunner(
        AppDbContext dbContext,
        IAuditLogger auditLogger,
        IUsageAggregator usageAggregator,
        IComplianceEvaluator complianceEvaluator,
        IEmailNotificationService notificationService,
        IReportDeliveryService reportDeliveryService,
        IBootstrapSettingsStore settingsStore,
        ILogger<BackgroundJobRunner> logger)
    {
        _dbContext = dbContext;
        _auditLogger = auditLogger;
        _usageAggregator = usageAggregator;
        _complianceEvaluator = complianceEvaluator;
        _notificationService = notificationService;
        _reportDeliveryService = reportDeliveryService;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public Task RunUsageAggregationAsync(string? correlationId = null)
    {
        return RunJobAsync(
            JobKeys.UsageAggregation,
            "Usage.JobRan",
            RunUsageAggregationCoreAsync,
            correlationId);
    }

    public Task RunComplianceEvaluationAsync(string? correlationId = null)
    {
        return RunJobAsync(
            JobKeys.ComplianceEvaluation,
            "Compliance.JobRan",
            RunComplianceEvaluationCoreAsync,
            correlationId);
    }

    public Task RunNotificationsAsync(string? correlationId = null)
    {
        return RunJobAsync(
            JobKeys.Notifications,
            "Notifications.JobRan",
            RunNotificationsCoreAsync,
            correlationId);
    }

    public Task RunAuditRetentionAsync(string? correlationId = null)
    {
        return RunJobAsync(
            JobKeys.AuditRetention,
            "Audit.RetentionCleanup",
            RunAuditRetentionCoreAsync,
            correlationId);
    }

    public async Task RunScheduledJobAsync(string jobKey, string? correlationId = null)
    {
        var definition = await _dbContext.ScheduledJobs.AsNoTracking()
            .FirstOrDefaultAsync(job => job.Key == jobKey);

        if (definition is null)
        {
            await RunJobAsync(jobKey, "Jobs.JobRan", _ =>
                throw new InvalidOperationException($"Job definition not found for {jobKey}."), correlationId);
            return;
        }

        switch (definition.JobType)
        {
            case JobKeys.UsageAggregation:
                await RunJobAsync(jobKey, "Usage.JobRan", RunUsageAggregationCoreAsync, correlationId);
                break;
            case JobKeys.ComplianceEvaluation:
                await RunJobAsync(jobKey, "Compliance.JobRan", RunComplianceEvaluationCoreAsync, correlationId);
                break;
            case JobKeys.Notifications:
                await RunJobAsync(jobKey, "Notifications.JobRan", RunNotificationsCoreAsync, correlationId);
                break;
            case JobKeys.ReportDelivery:
                await RunJobAsync(jobKey, "Reports.DeliveryJobRan",
                    token => _reportDeliveryService.DeliverAsync(jobKey, definition.ParametersJson, token), correlationId);
                break;
            case JobKeys.AuditRetention:
                await RunJobAsync(jobKey, "Audit.RetentionCleanup", RunAuditRetentionCoreAsync, correlationId);
                break;
            default:
                await RunJobAsync(jobKey, "Jobs.JobRan", _ =>
                    throw new InvalidOperationException($"Unsupported job type {definition.JobType}."), correlationId);
                break;
        }
    }

    private async Task RunJobAsync(
        string jobKey,
        string auditAction,
        Func<CancellationToken, Task<string>> action,
        string? correlationId)
    {
        var resolvedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;

        CorrelationContext.Current = resolvedCorrelationId;
        _logger.LogInformation("Job {JobKey} started. Correlation {CorrelationId}.", jobKey, resolvedCorrelationId);

        var logEntry = new JobExecutionLog
        {
            Id = Guid.NewGuid(),
            JobKey = jobKey,
            StartedAtUtc = DateTime.UtcNow,
            Status = "Running",
            CorrelationId = resolvedCorrelationId
        };

        _dbContext.JobExecutionLogs.Add(logEntry);
        await _dbContext.SaveChangesAsync();

        try
        {
            var summary = await action(CancellationToken.None);
            logEntry.Status = "Success";
            logEntry.Summary = TrimToLength(summary, 500);
            logEntry.FinishedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await WriteAuditAsync(auditAction, jobKey, logEntry.Summary ?? "Job completed.", resolvedCorrelationId);
            _logger.LogInformation("Job {JobKey} completed successfully. Correlation {CorrelationId}.", jobKey, resolvedCorrelationId);
        }
        catch (Exception ex)
        {
            logEntry.Status = "Failed";
            logEntry.Summary = "Job failed.";
            logEntry.Error = TrimToLength(SanitizeError(ex), 1000);
            logEntry.FinishedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            await WriteAuditAsync(auditAction, jobKey, $"Job failed: {logEntry.Error}", resolvedCorrelationId);
            _logger.LogError(ex, "Job {JobKey} failed. Correlation {CorrelationId}.", jobKey, resolvedCorrelationId);
            throw;
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    private Task WriteAuditAsync(string action, string jobKey, string summary, string correlationId)
    {
        return _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = SystemActor,
            ActorEmail = SystemActor,
            ActorDisplay = "System",
            CorrelationId = correlationId,
            Action = action,
            EntityType = "JobExecutionLog",
            EntityId = jobKey,
            Summary = TrimToLength(summary, 500),
            IpAddress = null
        });
    }

    private static string SanitizeError(Exception ex)
    {
        var message = ex.Message.Replace(Environment.NewLine, " ").Replace("\r", " ").Replace("\n", " ");
        if (ex.InnerException is not null)
        {
            message = $"{message} | {ex.InnerException.Message}";
        }

        return message;
    }

    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private async Task<string> RunUsageAggregationCoreAsync(CancellationToken cancellationToken)
    {
        var result = await _usageAggregator.AggregateAsync(null, null, cancellationToken);
        return $"Aggregated usage for {result.WindowStart:yyyy-MM-dd} to {result.WindowEnd:yyyy-MM-dd}. Licenses processed: {result.LicensesProcessed}. Summaries updated: {result.SummariesUpdated}.";
    }

    private async Task<string> RunComplianceEvaluationCoreAsync(CancellationToken cancellationToken)
    {
        var result = await _complianceEvaluator.EvaluateAsync(null, null, cancellationToken);
        return $"Evaluated compliance for {result.WindowStart:yyyy-MM-dd} to {result.WindowEnd:yyyy-MM-dd}. Opened: {result.Opened}, Resolved: {result.Resolved}, Updated: {result.Updated}.";
    }

    private async Task<string> RunNotificationsCoreAsync(CancellationToken cancellationToken)
    {
        return await _notificationService.RunDigestAsync(cancellationToken);
    }

    private async Task<string> RunAuditRetentionCoreAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        var retentionDays = NormalizeRetentionDays(settings.Audit.RetentionDays);
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var removed = await _dbContext.AuditLogs
            .Where(l => l.OccurredAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        return $"Purged {removed} audit logs older than {retentionDays} days.";
    }

    private static int NormalizeRetentionDays(int retentionDays)
        => retentionDays is 30 or 90 or 180 or 365 ? retentionDays : 180;
}
