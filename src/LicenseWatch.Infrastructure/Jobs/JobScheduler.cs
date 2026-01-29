using Hangfire;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Infrastructure.Jobs;

public interface IJobScheduler
{
    Task EnsureDefaultsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledJobDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ScheduledJobDefinition definition, string userId, CancellationToken cancellationToken = default);
    Task ToggleAsync(string jobKey, bool isEnabled, string userId, CancellationToken cancellationToken = default);
    Task SyncAsync(CancellationToken cancellationToken = default);
}

public sealed class JobScheduler : IJobScheduler
{
    private readonly AppDbContext _dbContext;
    private readonly IRecurringJobManager _recurringJobs;
    private readonly ILogger<JobScheduler> _logger;

    public JobScheduler(AppDbContext dbContext, IRecurringJobManager recurringJobs, ILogger<JobScheduler> logger)
    {
        _dbContext = dbContext;
        _recurringJobs = recurringJobs;
        _logger = logger;
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var existingKeys = await _dbContext.ScheduledJobs.AsNoTracking()
            .Select(job => job.Key)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var created = 0;

        foreach (var definition in JobCatalog.BuiltIn)
        {
            if (existingKeys.Contains(definition.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            _dbContext.ScheduledJobs.Add(new ScheduledJobDefinition
            {
                Id = Guid.NewGuid(),
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                JobType = definition.JobType,
                CronExpression = definition.DefaultCron,
                IsEnabled = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = "system"
            });

            created++;
        }

        if (created > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ScheduledJobDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ScheduledJobs.AsNoTracking()
            .OrderBy(job => job.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(ScheduledJobDefinition definition, string userId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ScheduledJobs
            .FirstOrDefaultAsync(job => job.Key == definition.Key, cancellationToken);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            definition.Id = Guid.NewGuid();
            definition.CreatedAtUtc = now;
            definition.UpdatedAtUtc = now;
            definition.CreatedByUserId = userId;
            _dbContext.ScheduledJobs.Add(definition);
        }
        else
        {
            existing.Name = definition.Name;
            existing.Description = definition.Description;
            existing.JobType = definition.JobType;
            existing.CronExpression = definition.CronExpression;
            existing.ParametersJson = definition.ParametersJson;
            existing.IsEnabled = definition.IsEnabled;
            existing.UpdatedAtUtc = now;
            existing.UpdatedByUserId = userId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleAsync(string jobKey, bool isEnabled, string userId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ScheduledJobs
            .FirstOrDefaultAsync(job => job.Key == jobKey, cancellationToken);

        if (existing is null)
        {
            return;
        }

        existing.IsEnabled = isEnabled;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await _dbContext.ScheduledJobs.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var definition in definitions)
        {
            try
            {
                if (!definition.IsEnabled)
                {
                    _recurringJobs.RemoveIfExists(definition.Key);
                    continue;
                }

                _recurringJobs.AddOrUpdate<BackgroundJobRunner>(
                    definition.Key,
                    job => job.RunScheduledJobAsync(definition.Key),
                    definition.CronExpression,
                    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to sync scheduled job {JobKey}", definition.Key);
            }
        }
    }
}

