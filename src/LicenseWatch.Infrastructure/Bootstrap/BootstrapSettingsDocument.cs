using System.Text.Json.Serialization;

namespace LicenseWatch.Infrastructure.Bootstrap;

internal class BootstrapSettingsDocument
{
    [JsonPropertyName("appName")]
    public string AppName { get; set; } = "License Watch";

    [JsonPropertyName("environmentLabel")]
    public string? EnvironmentLabel { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("protectedAppDbConnectionString")]
    public string? ProtectedAppDbConnectionString { get; set; }

    [JsonPropertyName("email")]
    public BootstrapEmailSettingsDocument Email { get; set; } = new();

    [JsonPropertyName("branding")]
    public BootstrapBrandingSettingsDocument Branding { get; set; } = new();

    [JsonPropertyName("compliance")]
    public BootstrapComplianceSettingsDocument Compliance { get; set; } = new();

    [JsonPropertyName("audit")]
    public BootstrapAuditSettingsDocument Audit { get; set; } = new();

    [JsonPropertyName("lastSavedUtc")]
    public DateTime LastSavedUtc { get; set; }
}

internal class BootstrapBrandingSettingsDocument
{
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("logoFileName")]
    public string? LogoFileName { get; set; }
}

internal class BootstrapEmailSettingsDocument
{
    [JsonPropertyName("smtpHost")]
    public string? SmtpHost { get; set; }

    [JsonPropertyName("smtpPort")]
    public int SmtpPort { get; set; } = 587;

    [JsonPropertyName("useSsl")]
    public bool UseSsl { get; set; } = true;

    [JsonPropertyName("ignoreTlsErrors")]
    public bool IgnoreTlsErrors { get; set; }

    [JsonPropertyName("enableDailySummary")]
    public bool EnableDailySummary { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("protectedPassword")]
    public string? ProtectedPassword { get; set; }

    [JsonPropertyName("fromName")]
    public string? FromName { get; set; }

    [JsonPropertyName("fromEmail")]
    public string? FromEmail { get; set; }

    [JsonPropertyName("defaultToEmail")]
    public string? DefaultToEmail { get; set; }

    [JsonPropertyName("suppressionMinutes")]
    public int SuppressionMinutes { get; set; } = 60;
}

internal class BootstrapComplianceSettingsDocument
{
    [JsonPropertyName("criticalDays")]
    public int CriticalDays { get; set; } = 30;

    [JsonPropertyName("warningDays")]
    public int WarningDays { get; set; } = 90;
}

internal class BootstrapAuditSettingsDocument
{
    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; } = 180;
}
