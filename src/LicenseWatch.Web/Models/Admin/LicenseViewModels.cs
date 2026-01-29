namespace LicenseWatch.Web.Models.Admin;

public class LicenseListItemViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime? ExpiresOnUtc { get; set; }
    public string Status { get; set; } = "Unknown";
}

public class LicenseListViewModel
{
    public IReadOnlyCollection<LicenseListItemViewModel> Licenses { get; set; } = Array.Empty<LicenseListItemViewModel>();
    public IReadOnlyCollection<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
    public Guid? CategoryId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int? ExpiringDays { get; set; }
    public bool OverAllocated { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class CategoryOption
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class LicenseFormViewModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public Guid CategoryId { get; set; }
    public int? SeatsPurchased { get; set; }
    public int? SeatsAssigned { get; set; }
    public decimal? CostPerSeatMonthly { get; set; }
    public string? Currency { get; set; }
    public DateTime? ExpiresOnUtc { get; set; }
    public string? Notes { get; set; }
    public bool UseCustomThresholds { get; set; }
    public int? CriticalThresholdDays { get; set; }
    public int? WarningThresholdDays { get; set; }
    public IReadOnlyCollection<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
}

public class LicenseDetailViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime? ExpiresOnUtc { get; set; }
    public string Status { get; set; } = "Unknown";
    public int? SeatsPurchased { get; set; }
    public int? SeatsAssigned { get; set; }
    public decimal? CostPerSeatMonthly { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
    public bool UseCustomThresholds { get; set; }
    public int? CriticalThresholdDays { get; set; }
    public int? WarningThresholdDays { get; set; }
    public DateTime? LastEvaluatedAtUtc { get; set; }
    public string ThresholdLabel { get; set; } = "System thresholds";
    public IReadOnlyCollection<AttachmentItemViewModel> Attachments { get; set; } = Array.Empty<AttachmentItemViewModel>();
    public IReadOnlyCollection<AuditLogItemViewModel> AuditLogs { get; set; } = Array.Empty<AuditLogItemViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class AttachmentItemViewModel
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public class AuditLogItemViewModel
{
    public DateTime OccurredAtUtc { get; set; }
    public string ActorEmail { get; set; } = string.Empty;
    public string ActorDisplay { get; set; } = string.Empty;
    public string? ActingAs { get; set; }
    public bool IsImpersonated { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string EntityIdDisplay { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string IpAddressDisplay { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Outcome { get; set; } = "Info";
    public string? CorrelationId { get; set; }
}
