namespace LicenseWatch.Infrastructure.Storage;

public class AttachmentStorageOptions
{
    public string RootPath { get; set; } = string.Empty;
    public int MaxSizeMb { get; set; } = 10;
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
}
