namespace LicenseWatch.Infrastructure.Dashboard;

public interface IDashboardQueryService
{
    Task<DashboardSnapshot> GetSnapshotAsync(int? rangeDays = null, CancellationToken cancellationToken = default);
}
