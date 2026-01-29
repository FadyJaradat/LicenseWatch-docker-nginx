using LicenseWatch.Core.Models;
using Microsoft.AspNetCore.Http;

namespace LicenseWatch.Web.Models.Admin;

public class SettingsViewModel
{
    public BootstrapSettingsInputModel Input { get; set; } = new();
    public string EnvironmentName { get; set; } = string.Empty;
    public string EffectiveAppDbConnectionString { get; set; } = string.Empty;
    public DateTime? LastSavedUtc { get; set; }
    public string? BrandingLogoUrl { get; set; }
    public string? AlertMessage { get; set; }
    public string AlertStyle { get; set; } = "info";
    public string? AlertDetails { get; set; }
}

public class BootstrapSettingsInputModel
{
    public string AppName { get; set; } = "License Watch";
    public string? EnvironmentLabel { get; set; }
    public string? AppDbConnectionString { get; set; }
    public string? Notes { get; set; }
    public string CompanyName { get; set; } = "LicenseWatch";
    public IFormFile? LogoFile { get; set; }
    public int ComplianceCriticalDays { get; set; } = 30;
    public int ComplianceWarningDays { get; set; } = 90;
    public int AuditRetentionDays { get; set; } = 180;
}
