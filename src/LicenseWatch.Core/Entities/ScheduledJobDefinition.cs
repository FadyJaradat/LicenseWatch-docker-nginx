namespace LicenseWatch.Core.Entities;

public class ScheduledJobDefinition
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string JobType { get; set; } = string.Empty;

    public string CronExpression { get; set; } = string.Empty;

    public string? ParametersJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? UpdatedByUserId { get; set; }
}
