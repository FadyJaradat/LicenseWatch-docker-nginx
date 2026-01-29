namespace LicenseWatch.Infrastructure.Dashboard;

public record DashboardKpis(
    int TotalLicenses,
    int Expired,
    int ExpiringSoon,
    int? OverAllocated,
    int OveruseRisk);

public record ExpiringSoonItem(Guid LicenseId, string Name, string CategoryName, DateTime ExpiresOnUtc, int DaysLeft, string Status);

public record StatusCount(string Status, int Count);

public record MonthlyExpirationCount(string Label, int Count);

public record RecentAuditItem(DateTime OccurredAtUtc, string ActorEmail, string Action, string Summary, string EntityType, string EntityId);

public record AttentionItem(string Severity, string Title, string Summary, Guid? LicenseId, string? TargetUrl);

public record VendorCount(string Vendor, int Count);

public record TrendBucket(string Label, int Count, int ExpiringDays);

public record DashboardSnapshot(
    DashboardKpis Kpis,
    IReadOnlyCollection<ExpiringSoonItem> ExpiringSoon,
    IReadOnlyCollection<AttentionItem> Attention,
    IReadOnlyCollection<StatusCount> StatusCounts,
    IReadOnlyCollection<MonthlyExpirationCount> ExpirationsByMonth,
    IReadOnlyCollection<TrendBucket> ExpirationTrend,
    IReadOnlyCollection<VendorCount> TopVendors,
    IReadOnlyCollection<RecentAuditItem> RecentActivity);
