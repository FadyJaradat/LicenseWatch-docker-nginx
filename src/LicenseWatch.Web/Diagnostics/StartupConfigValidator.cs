using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Web.Diagnostics;

public sealed class StartupConfigValidator : IHostedService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly StartupConfigState _state;
    private readonly ILogger<StartupConfigValidator> _logger;

    public StartupConfigValidator(
        IConfiguration configuration,
        IHostEnvironment environment,
        StartupConfigState state,
        ILogger<StartupConfigValidator> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _state = state;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ValidateConnectionString("IdentityDb", "Identity database");
        ValidateConnectionString("AppDb", "Application database");

        var seedEnabled = _configuration.GetValue<bool>("Security:SeedAdmin:Enabled");
        if (seedEnabled)
        {
            var seedEmail = _configuration["Security:SeedAdmin:Email"];
            var seedPassword = _configuration["Security:SeedAdmin:Password"];
            if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
            {
                _state.Add(
                    "Security:SeedAdmin",
                    "Seed admin is enabled but email or password is missing.",
                    StartupConfigSeverity.Error);
            }
        }

        if (!_environment.IsDevelopment() && seedEnabled)
        {
            _state.Add(
                "Security:SeedAdmin",
                "Seed admin is enabled in a non-development environment. Disable before production.",
                StartupConfigSeverity.Warning);
        }

        if (_state.Issues.Count > 0)
        {
            foreach (var issue in _state.Issues)
            {
                if (issue.Severity == StartupConfigSeverity.Error)
                {
                    _logger.LogError("Startup configuration issue: {Key} - {Message}", issue.Key, issue.Message);
                }
                else
                {
                    _logger.LogWarning("Startup configuration warning: {Key} - {Message}", issue.Key, issue.Message);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ValidateConnectionString(string name, string label)
    {
        var value = _configuration.GetConnectionString(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            _state.Add(
                $"ConnectionStrings:{name}",
                $"{label} connection string is missing.",
                StartupConfigSeverity.Error);
        }
    }
}
