using System.Text;
using LicenseWatch.Core.Models;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.SettingsManage)]
[Route("admin/settings")]
public class SettingsController : Controller
{
    private readonly IBootstrapSettingsStore _store;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        IBootstrapSettingsStore store,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<SettingsController> logger)
    {
        _store = store;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var settings = await _store.LoadAsync();
        var vm = BuildViewModel(settings);
        return View(vm);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(BootstrapSettingsInputModel input)
    {
        var existing = await _store.LoadAsync();
        var branding = existing.Branding;
        if (!string.IsNullOrWhiteSpace(input.CompanyName))
        {
            branding.CompanyName = input.CompanyName.Trim();
        }

        if (input.LogoFile is not null && input.LogoFile.Length > 0)
        {
            var fileName = await SaveLogoAsync(input.LogoFile);
            branding.LogoFileName = fileName;
        }

        var settings = new BootstrapSettings
        {
            AppName = input.AppName,
            EnvironmentLabel = input.EnvironmentLabel,
            Notes = input.Notes,
            AppDbConnectionString = string.IsNullOrWhiteSpace(input.AppDbConnectionString)
                ? _configuration.GetConnectionString("AppDb")
                : input.AppDbConnectionString,
            Email = existing.Email,
            Branding = branding,
            Compliance = new ComplianceSettings
            {
                CriticalDays = input.ComplianceCriticalDays,
                WarningDays = input.ComplianceWarningDays
            },
            Audit = new AuditSettings
            {
                RetentionDays = input.AuditRetentionDays
            },
            LastSavedUtc = DateTime.UtcNow
        };

        var validation = await _store.ValidateAsync(settings);
        if (!validation.IsValid)
        {
            var vmInvalid = BuildViewModel(settings);
            vmInvalid.AlertMessage = "Please correct the highlighted issues.";
            vmInvalid.AlertStyle = "danger";
            vmInvalid.AlertDetails = string.Join(Environment.NewLine, validation.Errors);
            ModelState.AddModelError(string.Empty, vmInvalid.AlertMessage);
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View("Index", vmInvalid);
        }

        await _store.SaveAsync(settings);
        var vm = BuildViewModel(settings);
        vm.AlertMessage = "Settings saved successfully.";
        vm.AlertStyle = "success";
        return View("Index", vm);
    }

    [HttpPost("test-connection")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(BootstrapSettingsInputModel input)
    {
        var currentSettings = await _store.LoadAsync();
        var effectiveConnectionString = !string.IsNullOrWhiteSpace(input.AppDbConnectionString)
            ? input.AppDbConnectionString
            : currentSettings.AppDbConnectionString ?? _configuration.GetConnectionString("AppDb") ?? string.Empty;

        var vm = BuildViewModel(currentSettings);
        vm.Input.AppDbConnectionString = input.AppDbConnectionString;

        if (string.IsNullOrWhiteSpace(effectiveConnectionString))
        {
            vm.AlertMessage = "Connection string is missing.";
            vm.AlertStyle = "danger";
            return View("Index", vm);
        }

        try
        {
            using var connection = new SqliteConnection(effectiveConnectionString);
            await connection.OpenAsync();
            vm.AlertMessage = "Connection succeeded.";
            vm.AlertStyle = "success";
        }
        catch (Exception ex)
        {
            vm.AlertMessage = "Connection failed.";
            vm.AlertStyle = "danger";
            vm.AlertDetails = SanitizeException(ex);
            _logger.LogError(ex, "AppDb connection test failed.");
        }

        return View("Index", vm);
    }

    private SettingsViewModel BuildViewModel(BootstrapSettings settings)
    {
        var effectiveConnection = settings.AppDbConnectionString ?? _configuration.GetConnectionString("AppDb") ?? string.Empty;
        var logoUrl = string.IsNullOrWhiteSpace(settings.Branding.LogoFileName)
            ? null
            : $"/app-data/branding/{settings.Branding.LogoFileName}";

        return new SettingsViewModel
        {
            Input = new BootstrapSettingsInputModel
            {
                AppName = string.IsNullOrWhiteSpace(settings.AppName) ? "License Watch" : settings.AppName,
                EnvironmentLabel = settings.EnvironmentLabel,
                AppDbConnectionString = settings.AppDbConnectionString,
                Notes = settings.Notes,
                CompanyName = string.IsNullOrWhiteSpace(settings.Branding.CompanyName)
                    ? "LicenseWatch"
                    : settings.Branding.CompanyName,
                ComplianceCriticalDays = settings.Compliance.CriticalDays,
                ComplianceWarningDays = settings.Compliance.WarningDays,
                AuditRetentionDays = settings.Audit.RetentionDays
            },
            EnvironmentName = _environment.EnvironmentName,
            EffectiveAppDbConnectionString = effectiveConnection,
            LastSavedUtc = settings.LastSavedUtc == default ? null : settings.LastSavedUtc,
            BrandingLogoUrl = logoUrl
        };
    }

    private async Task<string> SaveLogoAsync(IFormFile file)
    {
        var brandingPath = Path.Combine(_environment.ContentRootPath, "App_Data", "branding");
        Directory.CreateDirectory(brandingPath);

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        var safeFileName = $"logo-{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(brandingPath, safeFileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        return safeFileName;
    }

    private static string SanitizeException(Exception ex)
    {
        var builder = new StringBuilder();
        builder.Append(ex.Message);
        if (ex.InnerException is not null)
        {
            builder.Append(" | ").Append(ex.InnerException.Message);
        }
        return builder.ToString();
    }
}
