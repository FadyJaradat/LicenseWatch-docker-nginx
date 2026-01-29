namespace LicenseWatch.Infrastructure.Maintenance;

public sealed record BackupFileInfo(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc);
