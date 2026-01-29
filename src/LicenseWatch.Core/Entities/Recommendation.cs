namespace LicenseWatch.Core.Entities;

public class Recommendation
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public decimal? EstimatedMonthlySavings { get; set; }

    public decimal? EstimatedAnnualSavings { get; set; }

    public string Currency { get; set; } = "USD";

    public Guid? LicenseId { get; set; }

    public License? License { get; set; }

    public Guid? OptimizationInsightId { get; set; }

    public OptimizationInsight? OptimizationInsight { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
}
