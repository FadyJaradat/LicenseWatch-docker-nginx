using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace LicenseWatch.Infrastructure.Bootstrap;

public sealed class BootstrapSettingsHealthCheck : IHealthCheck
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector;
    private readonly string _filePath;

    public BootstrapSettingsHealthCheck(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<BootstrapSettingsStorageOptions> options)
    {
        _protector = dataProtectionProvider.CreateProtector("LicenseWatch.BootstrapSettings");
        _filePath = options.Value.FilePath;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_filePath))
        {
            return HealthCheckResult.Unhealthy("Bootstrap settings path is not configured.");
        }

        if (!File.Exists(_filePath))
        {
            return HealthCheckResult.Healthy("Bootstrap settings file not present.");
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var document = await JsonSerializer.DeserializeAsync<BootstrapSettingsDocument>(stream, SerializerOptions, cancellationToken)
                            ?? new BootstrapSettingsDocument();

            UnprotectIfPresent(document.ProtectedAppDbConnectionString);
            UnprotectIfPresent(document.Email.ProtectedPassword);

            return HealthCheckResult.Healthy("Bootstrap settings decrypted.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Bootstrap settings cannot be decrypted.", ex);
        }
    }

    private void UnprotectIfPresent(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return;
        }

        _protector.Unprotect(protectedValue);
    }
}
