namespace LicenseWatch.Core.Entities;

public class ImportSession
{
    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    public int NewLicenses { get; set; }

    public int UpdatedLicenses { get; set; }

    public int NewCategories { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<ImportRow> Rows { get; set; } = new List<ImportRow>();
}
