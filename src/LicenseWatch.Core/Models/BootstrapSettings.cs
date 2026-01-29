namespace LicenseWatch.Core.Models;

public class BootstrapSettings
{
    public string AppName { get; set; } = "License Watch";
    public string? EnvironmentLabel { get; set; }
    public string? AppDbConnectionString { get; set; }
    public string? Notes { get; set; }
    public EmailSettings Email { get; set; } = new();
    public BrandingSettings Branding { get; set; } = new();
    public ComplianceSettings Compliance { get; set; } = new();
    public AuditSettings Audit { get; set; } = new();
    public DateTime LastSavedUtc { get; set; }
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public bool IgnoreTlsErrors { get; set; }
    public bool EnableDailySummary { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string? DefaultToEmail { get; set; }
    public int SuppressionMinutes { get; set; } = 60;
}

public class BrandingSettings
{
    public string CompanyName { get; set; } = "LicenseWatch";
    public string? LogoFileName { get; set; }
}

public class ComplianceSettings
{
    public int CriticalDays { get; set; } = 30;
    public int WarningDays { get; set; } = 90;
}

public class AuditSettings
{
    public int RetentionDays { get; set; } = 180;
}
