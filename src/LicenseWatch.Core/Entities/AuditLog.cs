namespace LicenseWatch.Core.Entities;

public class AuditLog
{
    public Guid Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorEmail { get; set; } = string.Empty;

    public string? ActorDisplay { get; set; }

    public string? ImpersonatedUserId { get; set; }

    public string? ImpersonatedDisplay { get; set; }

    public string? CorrelationId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
}
