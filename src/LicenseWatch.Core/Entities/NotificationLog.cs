namespace LicenseWatch.Core.Entities;

public class NotificationLog
{
    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string Type { get; set; } = string.Empty;

    public string ToEmail { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Error { get; set; }

    public string? CorrelationId { get; set; }

    public string? TriggerEntityType { get; set; }

    public string? TriggerEntityId { get; set; }
}
