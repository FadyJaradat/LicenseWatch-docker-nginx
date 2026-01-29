using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LicenseWatch.Infrastructure.Health;

public sealed class WritableDirectoryHealthCheck : IHealthCheck
{
    private readonly string _directoryPath;
    private readonly string _displayName;

    public WritableDirectoryHealthCheck(string directoryPath, string displayName)
    {
        _directoryPath = directoryPath;
        _displayName = displayName;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_directoryPath))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"{_displayName} path is not configured."));
        }

        try
        {
            Directory.CreateDirectory(_directoryPath);

            var testFile = Path.Combine(_directoryPath, $"healthcheck-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "ok");
            File.Delete(testFile);

            return Task.FromResult(HealthCheckResult.Healthy($"{_displayName} is writable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"{_displayName} is not writable.", ex));
        }
    }
}
