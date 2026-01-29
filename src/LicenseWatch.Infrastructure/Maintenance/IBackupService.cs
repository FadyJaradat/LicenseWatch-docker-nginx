namespace LicenseWatch.Infrastructure.Maintenance;

public interface IBackupService
{
    string AppDataPath { get; }
    string BackupDirectory { get; }
    Task<BackupFileInfo> CreateBackupAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<BackupFileInfo> ListBackups(int maxCount = 20);
    string? ResolveBackupPath(string fileName);
}
