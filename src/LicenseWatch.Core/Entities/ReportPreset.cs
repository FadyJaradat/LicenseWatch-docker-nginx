namespace LicenseWatch.Core.Entities;

public class ReportPreset
{
    public Guid Id { get; set; }

    public string ReportKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string FiltersJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string UpdatedByUserId { get; set; } = string.Empty;
}
