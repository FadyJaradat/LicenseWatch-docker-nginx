namespace LicenseWatch.Web.Models.Admin;

public class SecurityDashboardViewModel
{
    public string EnvironmentName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public SecurityPostureViewModel Posture { get; set; } = new();
    public IReadOnlyList<SecurityEventViewModel> Events { get; set; } = Array.Empty<SecurityEventViewModel>();
}

public class SecurityPostureViewModel
{
    public bool CspEnabled { get; set; }
    public bool RateLimitingEnabled { get; set; }
    public bool HstsEnabled { get; set; }
    public string CookieSummary { get; set; } = string.Empty;
    public string PasswordSummary { get; set; } = string.Empty;
    public string LockoutSummary { get; set; } = string.Empty;
    public string RateLimitSummary { get; set; } = string.Empty;
}

public class SecurityEventViewModel
{
    public DateTime OccurredAtUtc { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? IpAddress { get; set; }
    public string? UserEmail { get; set; }
}
