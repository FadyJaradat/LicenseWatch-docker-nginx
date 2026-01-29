namespace LicenseWatch.Infrastructure.Reports;

public interface IReportsQueryService
{
    Task<PagedResult<LicenseReportRow>> GetLicenseInventoryAsync(LicenseReportFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LicenseReportRow>> GetLicenseInventoryExportAsync(LicenseReportFilter filter, CancellationToken cancellationToken = default);

    Task<PagedResult<ExpirationReportRow>> GetExpirationReportAsync(ExpirationReportFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpirationReportRow>> GetExpirationReportExportAsync(ExpirationReportFilter filter, CancellationToken cancellationToken = default);

    Task<PagedResult<ComplianceReportRow>> GetComplianceReportAsync(ComplianceReportFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComplianceReportRow>> GetComplianceReportExportAsync(ComplianceReportFilter filter, CancellationToken cancellationToken = default);

    Task<PagedResult<UsageReportRow>> GetUsageReportAsync(UsageReportFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UsageReportRow>> GetUsageReportExportAsync(UsageReportFilter filter, CancellationToken cancellationToken = default);
}
