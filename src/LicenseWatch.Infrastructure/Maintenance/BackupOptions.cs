namespace LicenseWatch.Infrastructure.Maintenance;

public class BackupOptions
{
    public string AppDataPath { get; set; } = string.Empty;
    public string BackupDirectory { get; set; } = string.Empty;
    public string[] ExcludedDirectories { get; set; } = Array.Empty<string>();
}
