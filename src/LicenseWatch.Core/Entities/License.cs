namespace LicenseWatch.Core.Entities;

public class License
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Vendor { get; set; }

    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }

    public int? SeatsPurchased { get; set; }

    public int? SeatsAssigned { get; set; }

    public decimal? CostPerSeatMonthly { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime? ExpiresOnUtc { get; set; }

    public string Status { get; set; } = "Unknown";

    public bool UseCustomThresholds { get; set; }

    public int? CriticalThresholdDays { get; set; }

    public int? WarningThresholdDays { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
