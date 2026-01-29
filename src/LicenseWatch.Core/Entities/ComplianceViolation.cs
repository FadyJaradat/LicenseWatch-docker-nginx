namespace LicenseWatch.Core.Entities;

public class ComplianceViolation
{
    public Guid Id { get; set; }

    public Guid? LicenseId { get; set; }

    public License? License { get; set; }

    public string RuleKey { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public string Status { get; set; } = "Open";

    public string Title { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public string EvidenceJson { get; set; } = string.Empty;

    public DateTime DetectedAtUtc { get; set; }

    public DateTime LastEvaluatedAtUtc { get; set; }

    public DateTime? AcknowledgedAtUtc { get; set; }

    public string? AcknowledgedByUserId { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }
}
