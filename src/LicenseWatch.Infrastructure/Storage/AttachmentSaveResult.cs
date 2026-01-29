namespace LicenseWatch.Infrastructure.Storage;

public record AttachmentSaveResult(bool Success, string? ErrorMessage, string? StoredFileName);
