namespace LicenseWatch.Core.Entities;

public class EmailNotificationRule
{
    public Guid Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Instant";
    public bool IsEnabled { get; set; }
    public string? RoleRecipients { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
}
