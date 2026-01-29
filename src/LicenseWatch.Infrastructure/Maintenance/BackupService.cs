using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LicenseWatch.Infrastructure.Maintenance;

public sealed class BackupService : IBackupService
{
    private readonly BackupOptions _options;
    private readonly ILogger<BackupService> _logger;

    public BackupService(IOptions<BackupOptions> options, ILogger<BackupService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string AppDataPath => _options.AppDataPath;
    public string BackupDirectory => _options.BackupDirectory;

    public async Task<BackupFileInfo> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.BackupDirectory);

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"licensewatch-backup-{stamp}.zip";
        var archivePath = Path.Combine(_options.BackupDirectory, fileName);

        var excluded = _options.ExcludedDirectories
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            .ToArray();

        var appDataFull = Path.GetFullPath(_options.AppDataPath);
        if (!Directory.Exists(appDataFull))
        {
            throw new DirectoryNotFoundException($"App_Data directory not found at {appDataFull}");
        }

        await using var stream = new FileStream(archivePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var file in Directory.EnumerateFiles(appDataFull, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = Path.GetFullPath(file);
            if (IsExcluded(fullPath, excluded))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(appDataFull, fullPath);
            if (relativePath.StartsWith("..", StringComparison.Ordinal))
            {
                continue;
            }

            var entryName = relativePath.Replace('\\', '/');
            archive.CreateEntryFromFile(fullPath, entryName, CompressionLevel.Optimal);
        }

        var info = new FileInfo(archivePath);
        _logger.LogInformation("Created backup {BackupFile} ({Size} bytes)", fileName, info.Length);
        return new BackupFileInfo(fileName, info.Length, info.CreationTimeUtc);
    }

    public IReadOnlyList<BackupFileInfo> ListBackups(int maxCount = 20)
    {
        if (!Directory.Exists(_options.BackupDirectory))
        {
            return Array.Empty<BackupFileInfo>();
        }

        return Directory.EnumerateFiles(_options.BackupDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.CreationTimeUtc)
            .Take(Math.Clamp(maxCount, 1, 100))
            .Select(info => new BackupFileInfo(info.Name, info.Length, info.CreationTimeUtc))
            .ToList();
    }

    public string? ResolveBackupPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var safeName = Path.GetFileName(fileName);
        if (!safeName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_options.BackupDirectory, safeName));
        if (!fullPath.StartsWith(Path.GetFullPath(_options.BackupDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(fullPath) ? fullPath : null;
    }

    private static bool IsExcluded(string path, string[] excluded)
    {
        foreach (var exclude in excluded)
        {
            if (path.StartsWith(exclude, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
