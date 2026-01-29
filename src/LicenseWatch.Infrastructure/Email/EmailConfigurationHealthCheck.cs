using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LicenseWatch.Infrastructure.Email;

public sealed class EmailConfigurationHealthCheck : IHealthCheck
{
    private readonly IBootstrapSettingsStore _settingsStore;
    private readonly AppDbContext _dbContext;

    public EmailConfigurationHealthCheck(IBootstrapSettingsStore settingsStore, AppDbContext dbContext)
    {
        _settingsStore = settingsStore;
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settingsStore.LoadAsync(cancellationToken);
            var email = settings.Email;

            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(email.SmtpHost))
            {
                missing.Add("SmtpHost");
            }
            if (email.SmtpPort <= 0)
            {
                missing.Add("SmtpPort");
            }
            if (string.IsNullOrWhiteSpace(email.FromName))
            {
                missing.Add("FromName");
            }
            if (string.IsNullOrWhiteSpace(email.FromEmail))
            {
                missing.Add("FromEmail");
            }

            if (missing.Count > 0)
            {
                return HealthCheckResult.Degraded(
                    "Email settings incomplete.",
                    data: new Dictionary<string, object>
                    {
                        ["missing"] = string.Join(", ", missing)
                    });
            }

            var lastFailure = await _dbContext.NotificationLogs.AsNoTracking()
                .Where(log => log.Status == "Failed")
                .OrderByDescending(log => log.CreatedAtUtc)
                .Select(log => new { log.CreatedAtUtc, log.Type })
                .FirstOrDefaultAsync(cancellationToken);

            if (lastFailure is not null)
            {
                return HealthCheckResult.Degraded(
                    $"Last email failure at {lastFailure.CreatedAtUtc:u}.",
                    data: new Dictionary<string, object>
                    {
                        ["last_failure_at"] = lastFailure.CreatedAtUtc,
                        ["last_failure_type"] = lastFailure.Type
                    });
            }

            return HealthCheckResult.Healthy("Email settings ready.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Email health check failed.", ex);
        }
    }
}
