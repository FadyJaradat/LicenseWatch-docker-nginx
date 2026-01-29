using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LicenseWatch.Web.Diagnostics;

public sealed class StartupConfigHealthCheck : IHealthCheck
{
    private readonly StartupConfigState _state;

    public StartupConfigHealthCheck(StartupConfigState state)
    {
        _state = state;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_state.Issues.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Startup configuration validated."));
        }

        var data = _state.Issues
            .Select((issue, index) => new KeyValuePair<string, object>($"issue_{index + 1}", $"{issue.Key}: {issue.Message} ({issue.Severity})"))
            .ToDictionary(pair => pair.Key, pair => (object)pair.Value);

        return Task.FromResult(HealthCheckResult.Degraded("Startup configuration issues detected.", data: data));
    }
}
