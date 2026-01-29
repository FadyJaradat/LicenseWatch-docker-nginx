namespace LicenseWatch.Web.Models.Admin;

public class AuditListViewModel
{
    public IReadOnlyCollection<AuditLogItemViewModel> Logs { get; set; } = Array.Empty<AuditLogItemViewModel>();
    public IReadOnlyCollection<SystemLogItemViewModel> SystemLogs { get; set; } = Array.Empty<SystemLogItemViewModel>();
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? CorrelationId { get; set; }
    public IReadOnlyList<string> ActionOptions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> EntityTypeOptions { get; set; } = Array.Empty<string>();
    public string? Search { get; set; }
    public int? RangeDays { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int TotalCount { get; set; }
    public int PageSize { get; set; } = 50;
    public string ActiveTab { get; set; } = "audit";
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public bool CanViewSensitiveDetails { get; set; }
}

public class SystemLogItemViewModel
{
    public DateTime OccurredAtUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Actor { get; set; } = "System";
    public string? ActingAs { get; set; }
    public bool IsImpersonated { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? DetailDisplay { get; set; }
    public bool IsRedacted { get; set; }
    public string? RedactionReason { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
}
