namespace LicenseWatch.Web.Models.Admin;

public class MaintenanceViewModel
{
    public string AppDataPath { get; set; } = string.Empty;
    public string BackupsPath { get; set; } = string.Empty;
    public IReadOnlyList<BackupItemViewModel> Backups { get; set; } = Array.Empty<BackupItemViewModel>();
    public string? AlertMessage { get; set; }
    public string? AlertStyle { get; set; }
    public string? AlertDetails { get; set; }
}

public class BackupItemViewModel
{
    public string FileName { get; set; } = string.Empty;
    public string SizeLabel { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
