namespace LicenseWatch.Web.Models.Admin;

public class MigrationAssistantViewModel
{
    public DateTime CheckedAtUtc { get; set; }
    public IReadOnlyList<MigrationContextViewModel> Contexts { get; set; } = Array.Empty<MigrationContextViewModel>();
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class MigrationContextViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> AppliedMigrations { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> PendingMigrations { get; set; } = Array.Empty<string>();
    public DateTime? LastAppliedUtc { get; set; }
    public string Status { get; set; } = "Unknown";
    public string? Guidance { get; set; }
    public bool CanApply { get; set; }
}
