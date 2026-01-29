namespace LicenseWatch.Infrastructure.Bootstrap;

public record BootstrapSettingsValidationResult(bool IsValid, IReadOnlyCollection<string> Errors)
{
    public static BootstrapSettingsValidationResult Success => new(true, Array.Empty<string>());
}
