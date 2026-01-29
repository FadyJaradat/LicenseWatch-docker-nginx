namespace LicenseWatch.Web.Models.Admin;

public class SystemStatusViewModel
{
    public string Version { get; set; } = string.Empty;
    public string? Commit { get; set; }
    public string? CommitShort { get; set; }
    public string BuildTimestamp { get; set; } = "Unknown";
    public string EnvironmentName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public string Uptime { get; set; } = string.Empty;
    public IReadOnlyList<SystemJobSummaryViewModel> Jobs { get; set; } = Array.Empty<SystemJobSummaryViewModel>();
    public IReadOnlyList<SystemHealthCheckViewModel> Checks { get; set; } = Array.Empty<SystemHealthCheckViewModel>();
    public DateTime CheckedAtUtc { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
}

public class SystemJobSummaryViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime? LastRunUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Summary { get; set; }
}

public class SystemHealthCheckViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Guidance { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Details { get; set; }
}
