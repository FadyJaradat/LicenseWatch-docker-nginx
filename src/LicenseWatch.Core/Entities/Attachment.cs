namespace LicenseWatch.Core.Entities;

public class Attachment
{
    public Guid Id { get; set; }

    public Guid LicenseId { get; set; }

    public License? License { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }
}
