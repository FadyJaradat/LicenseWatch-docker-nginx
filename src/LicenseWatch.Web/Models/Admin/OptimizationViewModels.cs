namespace LicenseWatch.Web.Models.Admin;

public class OptimizationOverviewViewModel
{
    public IReadOnlyList<OptimizationInsightItemViewModel> Insights { get; set; } = Array.Empty<OptimizationInsightItemViewModel>();
    public IReadOnlyCollection<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
    public string? Severity { get; set; }
    public string? Key { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Status { get; set; }
    public int ActiveInsights { get; set; }
    public int CriticalInsights { get; set; }
    public decimal? EstimatedAnnualSavings { get; set; }
    public int RecommendationsOpen { get; set; }
    public DateTime LastRefreshedUtc { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class OptimizationInsightItemViewModel
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? LicenseId { get; set; }
    public string? LicenseName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public string EvidenceSummary { get; set; } = string.Empty;
}

public class RecommendationListViewModel
{
    public IReadOnlyList<RecommendationListItemViewModel> Recommendations { get; set; } = Array.Empty<RecommendationListItemViewModel>();
    public IReadOnlyCollection<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
    public IReadOnlyCollection<LicenseOption> Licenses { get; set; } = Array.Empty<LicenseOption>();
    public string? Status { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? LicenseId { get; set; }
    public IReadOnlyList<string> StatusOptions { get; set; } = Array.Empty<string>();
    public DateTime LastRefreshedUtc { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class RecommendationListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? EstimatedAnnualSavings { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? LicenseId { get; set; }
    public string? LicenseName { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class RecommendationFormViewModel
{
    public Guid? Id { get; set; }
    public Guid? OptimizationInsightId { get; set; }
    public Guid? LicenseId { get; set; }
    public IReadOnlyCollection<LicenseOption> Licenses { get; set; } = Array.Empty<LicenseOption>();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public decimal? EstimatedMonthlySavings { get; set; }
    public decimal? EstimatedAnnualSavings { get; set; }
    public string Currency { get; set; } = "USD";
    public IReadOnlyList<string> StatusOptions { get; set; } = Array.Empty<string>();
    public string? InsightTitle { get; set; }
    public string? InsightEvidenceSummary { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class RecommendationDetailViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? EstimatedMonthlySavings { get; set; }
    public decimal? EstimatedAnnualSavings { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid? LicenseId { get; set; }
    public string? LicenseName { get; set; }
    public Guid? OptimizationInsightId { get; set; }
    public string? InsightTitle { get; set; }
    public string? InsightEvidenceSummary { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}
