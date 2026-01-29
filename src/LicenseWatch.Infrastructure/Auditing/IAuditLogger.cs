using LicenseWatch.Core.Entities;

namespace LicenseWatch.Infrastructure.Auditing;

public interface IAuditLogger
{
    Task LogAsync(AuditLog entry, CancellationToken cancellationToken = default);
}
