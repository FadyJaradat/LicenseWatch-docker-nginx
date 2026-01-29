using Microsoft.AspNetCore.Http;

namespace LicenseWatch.Infrastructure.Storage;

public interface IAttachmentStorage
{
    Task<AttachmentSaveResult> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
    string GetFilePath(string storedFileName);
    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);
}
