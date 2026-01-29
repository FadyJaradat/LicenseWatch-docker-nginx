namespace LicenseWatch.Core.Entities;

public class JobExecutionLog
{
    public Guid Id { get; set; }

    public string JobKey { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? Error { get; set; }

    public string? CorrelationId { get; set; }
}
