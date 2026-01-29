namespace LicenseWatch.Web.Models.Admin;

public class ComplianceListViewModel
{
    public int OpenCount { get; set; }

    public int CriticalCount { get; set; }

    public int WarningCount { get; set; }

    public int AcknowledgedCount { get; set; }

    public DateTime? LastEvaluatedAtUtc { get; set; }

    public string? Status { get; set; }

    public string? Severity { get; set; }

    public string? RuleKey { get; set; }

    public string? Search { get; set; }

    public IReadOnlyList<string> RuleKeys { get; set; } = Array.Empty<string>();

    public IReadOnlyList<ComplianceViolationViewModel> Violations { get; set; } = Array.Empty<ComplianceViolationViewModel>();

    public string? AlertMessage { get; set; }

    public string? AlertDetails { get; set; }

    public string AlertStyle { get; set; } = "info";
}

public class ComplianceViolationViewModel
{
    public Guid Id { get; set; }

    public Guid? LicenseId { get; set; }

    public string LicenseName { get; set; } = "Unknown license";

    public string? Vendor { get; set; }

    public string RuleKey { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public string Status { get; set; } = "Open";

    public string Title { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public DateTime DetectedAtUtc { get; set; }

    public DateTime LastEvaluatedAtUtc { get; set; }

    public IReadOnlyList<ComplianceEvidenceItemViewModel> EvidenceItems { get; set; } = Array.Empty<ComplianceEvidenceItemViewModel>();

    public bool CanSimulate { get; set; }
}

public class ComplianceEvidenceItemViewModel
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class ComplianceRunResultViewModel
{
    public DateOnly WindowStart { get; set; }

    public DateOnly WindowEnd { get; set; }

    public int Opened { get; set; }

    public int Resolved { get; set; }

    public int Updated { get; set; }

    public int TotalOpen { get; set; }

    public int TotalAcknowledged { get; set; }

    public int TotalResolved { get; set; }
}
