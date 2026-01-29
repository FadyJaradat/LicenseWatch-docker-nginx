namespace LicenseWatch.Web.Models.Admin;

using LicenseWatch.Web.Models.Ui;

public class DashboardViewModel
{
    public int TotalLicenses { get; set; }
    public int Expired { get; set; }
    public int ExpiringSoon { get; set; }
    public int OveruseRisk { get; set; }
    public int? OverAllocated { get; set; }
    public string? NextRenewalName { get; set; }
    public int? NextRenewalDays { get; set; }
    public DateTime? NextRenewalDateUtc { get; set; }
    public string? NextRenewalUrl { get; set; }
    public int RangeDays { get; set; } = 90;
    public DateTime LastRefreshedUtc { get; set; }
    public IReadOnlyCollection<KpiCardViewModel> Kpis { get; set; } = Array.Empty<KpiCardViewModel>();
    public IReadOnlyCollection<AttentionItemViewModel> CriticalAttention { get; set; } = Array.Empty<AttentionItemViewModel>();
    public IReadOnlyCollection<AttentionItemViewModel> WarningAttention { get; set; } = Array.Empty<AttentionItemViewModel>();
    public IReadOnlyCollection<TrendBucketViewModel> ExpirationTrend { get; set; } = Array.Empty<TrendBucketViewModel>();
    public IReadOnlyCollection<ChartPointViewModel> StatusCounts { get; set; } = Array.Empty<ChartPointViewModel>();
    public IReadOnlyCollection<VendorCountViewModel> VendorCounts { get; set; } = Array.Empty<VendorCountViewModel>();
    public IReadOnlyCollection<ActivityFeedItemViewModel> RecentActivity { get; set; } = Array.Empty<ActivityFeedItemViewModel>();

    public bool HasLicenses => TotalLicenses > 0;
}

public class AttentionItemViewModel
{
    public string Severity { get; set; } = "Warning";
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
}

public class TrendBucketViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ExpiringDays { get; set; }
}

public class VendorCountViewModel
{
    public string Vendor { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ChartPointViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}
