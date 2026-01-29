using LicenseWatch.Core.Models;

namespace LicenseWatch.Infrastructure.Bootstrap;

public interface IBootstrapSettingsStore
{
    Task<BootstrapSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task<BootstrapSettingsValidationResult> ValidateAsync(BootstrapSettings settings, CancellationToken cancellationToken = default);

    Task SaveAsync(BootstrapSettings settings, CancellationToken cancellationToken = default);
}
