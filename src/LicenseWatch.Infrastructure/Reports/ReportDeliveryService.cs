using System.Net.Mail;
using System.Text.Json;
using LicenseWatch.Core.Diagnostics;
using LicenseWatch.Core.Entities;
using LicenseWatch.Core.Reports;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Email;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Infrastructure.Reports;

public interface IReportDeliveryService
{
    Task<string> DeliverAsync(string jobKey, string? parametersJson, CancellationToken cancellationToken = default);
}

public sealed class ReportDeliveryService : IReportDeliveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly IReportsQueryService _reports;
    private readonly IReportExportService _exportService;
    private readonly IEmailSender _emailSender;
    private readonly IBootstrapSettingsStore _settingsStore;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<ReportDeliveryService> _logger;

    public ReportDeliveryService(
        AppDbContext dbContext,
        IReportsQueryService reports,
        IReportExportService exportService,
        IEmailSender emailSender,
        IBootstrapSettingsStore settingsStore,
        IAuditLogger auditLogger,
        ILogger<ReportDeliveryService> logger)
    {
        _dbContext = dbContext;
        _reports = reports;
        _exportService = exportService;
        _emailSender = emailSender;
        _settingsStore = settingsStore;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<string> DeliverAsync(string jobKey, string? parametersJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return "No report parameters configured.";
        }

        ReportSchedulePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ReportSchedulePayload>(parametersJson, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize report delivery parameters.");
            return "Invalid report parameters.";
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.ReportKey))
        {
            return "Invalid report configuration.";
        }

        var recipients = ParseRecipients(payload.Recipients);
        if (recipients.Count == 0)
        {
            return "No recipients configured for report delivery.";
        }

        var filtersJson = payload.FiltersJson;
        if (string.IsNullOrWhiteSpace(filtersJson) && payload.PresetId.HasValue)
        {
            var preset = await _dbContext.ReportPresets.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == payload.PresetId.Value, cancellationToken);
            filtersJson = preset?.FiltersJson;
        }

        var export = await BuildExportAsync(payload.ReportKey, payload.Format, filtersJson, cancellationToken);
        if (export is null)
        {
            return "Unsupported report configuration.";
        }

        var settings = await _settingsStore.LoadAsync(cancellationToken);
        var subject = $"{settings.AppName} report · {ResolveReportName(payload.ReportKey)}";
        var htmlBody = $"<h2>{settings.AppName}</h2><p>Your scheduled report is ready: <strong>{ResolveReportName(payload.ReportKey)}</strong>.</p><p class=\"small\">{BuildInfo.DisplayVersion}</p>";
        var textBody = $"{settings.AppName} report ready: {ResolveReportName(payload.ReportKey)}.{Environment.NewLine}Version: {BuildInfo.DisplayVersion}";
        var attachment = new EmailAttachment(export.FileName, export.ContentType, export.Content);

        var sent = 0;
        var correlationId = CorrelationContext.Current;
        foreach (var recipient in recipients)
        {
            var result = await _emailSender.SendAsync(
                recipient,
                subject,
                htmlBody,
                textBody,
                "ReportDelivery",
                correlationId,
                "ScheduledJob",
                jobKey,
                new[] { attachment },
                cancellationToken);

            if (result.Status == "Sent")
            {
                sent++;
            }
        }

        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = "system",
            ActorEmail = "system",
            ActorDisplay = "System",
            CorrelationId = correlationId,
            Action = "Reports.DeliverySent",
            EntityType = "ScheduledJob",
            EntityId = jobKey,
            Summary = $"Delivered {ResolveReportName(payload.ReportKey)} to {sent}/{recipients.Count} recipients.",
            IpAddress = null
        }, cancellationToken);

        return $"Report delivered to {sent}/{recipients.Count} recipients.";
    }

    private async Task<ReportExportResult?> BuildExportAsync(string reportKey, string format, string? filtersJson, CancellationToken cancellationToken)
    {
        format = string.IsNullOrWhiteSpace(format) ? "csv" : format;
        var fileExtension = format.Equals("excel", StringComparison.OrdinalIgnoreCase) ? "xlsx" : "csv";
        var fileName = $"report-{reportKey.ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd-HHmm}.{fileExtension}";

        switch (reportKey)
        {
            case ReportKeys.LicenseInventory:
            {
                var filter = BuildLicenseFilter(filtersJson);
                var rows = await _reports.GetLicenseInventoryExportAsync(filter, cancellationToken);
                return format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                    ? _exportService.ExportLicenseInventoryExcel(rows, fileName)
                    : _exportService.ExportLicenseInventoryCsv(rows, fileName);
            }
            case ReportKeys.Expirations:
            {
                var filter = BuildExpirationFilter(filtersJson);
                var rows = await _reports.GetExpirationReportExportAsync(filter, cancellationToken);
                return format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                    ? _exportService.ExportExpirationExcel(rows, fileName)
                    : _exportService.ExportExpirationCsv(rows, fileName);
            }
            case ReportKeys.Compliance:
            {
                var filter = BuildComplianceFilter(filtersJson);
                var rows = await _reports.GetComplianceReportExportAsync(filter, cancellationToken);
                return format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                    ? _exportService.ExportComplianceExcel(rows, fileName)
                    : _exportService.ExportComplianceCsv(rows, fileName);
            }
            case ReportKeys.Usage:
            {
                var filter = BuildUsageFilter(filtersJson);
                var rows = await _reports.GetUsageReportExportAsync(filter, cancellationToken);
                return format.Equals("excel", StringComparison.OrdinalIgnoreCase)
                    ? _exportService.ExportUsageExcel(rows, fileName)
                    : _exportService.ExportUsageCsv(rows, fileName);
            }
            default:
                return null;
        }
    }

    private static LicenseReportFilter BuildLicenseFilter(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new LicenseReportFilter(null, null, null, null, null);
        }

        var payload = JsonSerializer.Deserialize<LicensePresetPayload>(json, JsonOptions);
        return payload is null
            ? new LicenseReportFilter(null, null, null, null, null)
            : new LicenseReportFilter(payload.CategoryId, payload.Vendor, payload.Status, ToDateOnly(payload.ExpiresFrom), ToDateOnly(payload.ExpiresTo));
    }

    private static ExpirationReportFilter BuildExpirationFilter(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ExpirationReportFilter(null, null, null, null);
        }

        var payload = JsonSerializer.Deserialize<ExpirationPresetPayload>(json, JsonOptions);
        return payload is null
            ? new ExpirationReportFilter(null, null, null, null)
            : new ExpirationReportFilter(payload.CategoryId, payload.ExpiringDays, ToDateOnly(payload.ExpiresFrom), ToDateOnly(payload.ExpiresTo));
    }

    private static ComplianceReportFilter BuildComplianceFilter(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ComplianceReportFilter(null, null, null);
        }

        var payload = JsonSerializer.Deserialize<CompliancePresetPayload>(json, JsonOptions);
        return payload is null
            ? new ComplianceReportFilter(null, null, null)
            : new ComplianceReportFilter(payload.Status, payload.Severity, payload.Rule);
    }

    private static UsageReportFilter BuildUsageFilter(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UsageReportFilter(null, null, null, null);
        }

        var payload = JsonSerializer.Deserialize<UsagePresetPayload>(json, JsonOptions);
        return payload is null
            ? new UsageReportFilter(null, null, null, null)
            : new UsageReportFilter(payload.CategoryId, payload.LicenseId, ToDateOnly(payload.From), ToDateOnly(payload.To));
    }

    private static DateOnly? ToDateOnly(DateTime? value)
        => value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    private static List<string> ParseRecipients(string? recipients)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(recipients))
        {
            return new List<string>();
        }

        foreach (var part in recipients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsValidEmail(part))
            {
                results.Add(part);
            }
        }

        return results.ToList();
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveReportName(string reportKey)
        => reportKey switch
        {
            ReportKeys.LicenseInventory => "License inventory",
            ReportKeys.Expirations => "Expirations",
            ReportKeys.Compliance => "Compliance violations",
            ReportKeys.Usage => "Usage summary",
            _ => reportKey
        };

    private sealed record ReportSchedulePayload(
        string ReportKey,
        string Format,
        string? Recipients,
        Guid? PresetId,
        string? FiltersJson);

    private sealed record LicensePresetPayload(
        Guid? CategoryId,
        string? Vendor,
        string? Status,
        DateTime? ExpiresFrom,
        DateTime? ExpiresTo);

    private sealed record ExpirationPresetPayload(
        Guid? CategoryId,
        int? ExpiringDays,
        DateTime? ExpiresFrom,
        DateTime? ExpiresTo);

    private sealed record CompliancePresetPayload(
        string? Status,
        string? Severity,
        string? Rule);

    private sealed record UsagePresetPayload(
        Guid? CategoryId,
        Guid? LicenseId,
        DateTime? From,
        DateTime? To);
}
