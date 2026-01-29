namespace LicenseWatch.Core.Entities;

public class ImportRow
{
    public Guid Id { get; set; }

    public Guid ImportSessionId { get; set; }

    public ImportSession? ImportSession { get; set; }

    public int RowNumber { get; set; }

    public string? LicenseIdRaw { get; set; }

    public Guid? LicenseId { get; set; }

    public string LicenseName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string? Vendor { get; set; }

    public int? SeatsPurchased { get; set; }

    public int? SeatsAssigned { get; set; }

    public DateTime? ExpiresOnUtc { get; set; }

    public string? Notes { get; set; }

    public bool IsValid { get; set; }

    public string Action { get; set; } = "Invalid";

    public string? ErrorMessage { get; set; }
}
