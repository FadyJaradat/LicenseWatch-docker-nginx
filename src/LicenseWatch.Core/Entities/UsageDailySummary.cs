namespace LicenseWatch.Core.Entities;

public class UsageDailySummary
{
    public Guid Id { get; set; }

    public Guid LicenseId { get; set; }

    public License? License { get; set; }

    public DateTime UsageDateUtc { get; set; }

    public int MaxSeatsUsed { get; set; }
}
