namespace LicenseWatch.Core.Entities;

public class OptimizationInsight
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public Guid? LicenseId { get; set; }

    public License? License { get; set; }

    public Guid? CategoryId { get; set; }

    public Category? Category { get; set; }

    public DateTime DetectedAtUtc { get; set; }

    public string EvidenceJson { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
