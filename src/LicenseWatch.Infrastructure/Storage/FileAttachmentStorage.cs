using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LicenseWatch.Infrastructure.Storage;

public class FileAttachmentStorage : IAttachmentStorage
{
    private readonly AttachmentStorageOptions _options;
    private readonly ILogger<FileAttachmentStorage> _logger;
    private static readonly Dictionary<string, string[]> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new[] { "application/pdf" },
        [".png"] = new[] { "image/png" },
        [".jpg"] = new[] { "image/jpeg" },
        [".jpeg"] = new[] { "image/jpeg" },
        [".docx"] = new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        [".xlsx"] = new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        [".txt"] = new[] { "text/plain" }
    };
    private static readonly Dictionary<string, byte[]> SignatureHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = new byte[] { 0x25, 0x50, 0x44, 0x46 },
        [".png"] = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
        [".jpg"] = new byte[] { 0xFF, 0xD8, 0xFF },
        [".jpeg"] = new byte[] { 0xFF, 0xD8, 0xFF }
    };

    public FileAttachmentStorage(IOptions<AttachmentStorageOptions> options, ILogger<FileAttachmentStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
        Directory.CreateDirectory(_options.RootPath);
    }

    public async Task<AttachmentSaveResult> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            return new AttachmentSaveResult(false, "The file is empty.", null);
        }

        var maxBytes = _options.MaxSizeMb * 1024L * 1024L;
        if (file.Length > maxBytes)
        {
            return new AttachmentSaveResult(false, $"File exceeds {_options.MaxSizeMb} MB limit.", null);
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return new AttachmentSaveResult(false, "File type is not allowed.", null);
        }

        if (AllowedContentTypes.TryGetValue(extension, out var allowedTypes))
        {
            if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return new AttachmentSaveResult(false, "File content type does not match the extension.", null);
            }
        }

        Stream? extraStream = null;
        try
        {
            using var source = file.OpenReadStream();
            if (!await HasValidSignatureAsync(source, extension, cancellationToken))
            {
                return new AttachmentSaveResult(false, "File content does not match the extension.", null);
            }

            Stream copySource = source;
            if (source.CanSeek)
            {
                source.Position = 0;
            }
            else
            {
                extraStream = file.OpenReadStream();
                copySource = extraStream;
            }

            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var targetPath = Path.Combine(_options.RootPath, storedFileName);
            await using var stream = File.Create(targetPath);
            await copySource.CopyToAsync(stream, cancellationToken);

            return new AttachmentSaveResult(true, null, storedFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save attachment.");
            return new AttachmentSaveResult(false, "Failed to save the file.", null);
        }
        finally
        {
            if (extraStream is not null)
            {
                await extraStream.DisposeAsync();
            }
        }
    }

    public string GetFilePath(string storedFileName)
    {
        var safeName = Path.GetFileName(storedFileName);
        return Path.Combine(_options.RootPath, safeName);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var path = GetFilePath(storedFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static async Task<bool> HasValidSignatureAsync(Stream stream, string extension, CancellationToken cancellationToken)
    {
        if (!SignatureHeaders.TryGetValue(extension, out var signature))
        {
            return true;
        }

        var buffer = new byte[signature.Length];
        var read = await stream.ReadAsync(buffer.AsMemory(0, signature.Length), cancellationToken);
        if (read < signature.Length)
        {
            return false;
        }

        return buffer.SequenceEqual(signature);
    }
}
