namespace LicenseWatch.Infrastructure.Reports;

public sealed record LicenseReportFilter(
    Guid? CategoryId,
    string? Vendor,
    string? Status,
    DateOnly? ExpiresFrom,
    DateOnly? ExpiresTo);

public sealed record ExpirationReportFilter(
    Guid? CategoryId,
    int? ExpiringDays,
    DateOnly? ExpiresFrom,
    DateOnly? ExpiresTo);

public sealed record ComplianceReportFilter(
    string? Status,
    string? Severity,
    string? Rule);

public sealed record UsageReportFilter(
    Guid? CategoryId,
    Guid? LicenseId,
    DateOnly? From,
    DateOnly? To);

public sealed record LicenseReportRow(
    Guid Id,
    string Name,
    string? Vendor,
    string Category,
    int? SeatsPurchased,
    int? SeatsAssigned,
    DateTime? ExpiresOnUtc,
    string Status);

public sealed record ExpirationReportRow(
    Guid Id,
    string LicenseName,
    string? Vendor,
    DateTime ExpiresOnUtc,
    int DaysRemaining,
    string Status);

public sealed record ComplianceReportRow(
    Guid Id,
    string Severity,
    string Title,
    string Status,
    DateTime DetectedAtUtc,
    Guid? LicenseId,
    string? LicenseName,
    string RuleKey);

public sealed record UsageReportRow(
    Guid LicenseId,
    string LicenseName,
    string CategoryName,
    int PeakSeatsUsed,
    double AvgSeatsUsed,
    int EventsCount,
    DateOnly PeriodStart,
    DateOnly PeriodEnd);
