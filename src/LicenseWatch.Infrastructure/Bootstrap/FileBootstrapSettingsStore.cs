using System.Text.Json;
using LicenseWatch.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LicenseWatch.Infrastructure.Bootstrap;

public class FileBootstrapSettingsStore : IBootstrapSettingsStore
{
    private readonly IDataProtector _protector;
    private readonly string _filePath;
    private readonly ILogger<FileBootstrapSettingsStore> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FileBootstrapSettingsStore(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<BootstrapSettingsStorageOptions> options,
        ILogger<FileBootstrapSettingsStore> logger)
    {
        _protector = dataProtectionProvider.CreateProtector("LicenseWatch.BootstrapSettings");
        _filePath = options.Value.FilePath;
        _logger = logger;
    }

    public async Task<BootstrapSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Bootstrap settings not found at {Path}, returning defaults.", _filePath);
                return new BootstrapSettings { LastSavedUtc = DateTime.UtcNow };
            }

            await using var stream = File.OpenRead(_filePath);
            var document = await JsonSerializer.DeserializeAsync<BootstrapSettingsDocument>(stream, SerializerOptions, cancellationToken)
                            ?? new BootstrapSettingsDocument();

            return new BootstrapSettings
            {
                AppName = string.IsNullOrWhiteSpace(document.AppName) ? "License Watch" : document.AppName,
                EnvironmentLabel = document.EnvironmentLabel,
                Notes = document.Notes,
                AppDbConnectionString = Unprotect(document.ProtectedAppDbConnectionString),
                Branding = new BrandingSettings
                {
                    CompanyName = string.IsNullOrWhiteSpace(document.Branding.CompanyName)
                        ? "LicenseWatch"
                        : document.Branding.CompanyName,
                    LogoFileName = document.Branding.LogoFileName
                },
                Email = new EmailSettings
                {
                    SmtpHost = document.Email.SmtpHost ?? string.Empty,
                    SmtpPort = document.Email.SmtpPort == 0 ? 587 : document.Email.SmtpPort,
                    UseSsl = document.Email.UseSsl,
                    IgnoreTlsErrors = document.Email.IgnoreTlsErrors,
                    EnableDailySummary = document.Email.EnableDailySummary,
                    Username = document.Email.Username,
                    Password = Unprotect(document.Email.ProtectedPassword),
                    FromName = document.Email.FromName ?? string.Empty,
                    FromEmail = document.Email.FromEmail ?? string.Empty,
                    DefaultToEmail = document.Email.DefaultToEmail,
                    SuppressionMinutes = document.Email.SuppressionMinutes == 0 ? 60 : document.Email.SuppressionMinutes
                },
                Compliance = NormalizeComplianceSettings(document.Compliance),
                Audit = NormalizeAuditSettings(document.Audit),
                LastSavedUtc = document.LastSavedUtc
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load bootstrap settings from {Path}", _filePath);
            return new BootstrapSettings { AppName = "License Watch" };
        }
    }

    public Task<BootstrapSettingsValidationResult> ValidateAsync(BootstrapSettings settings, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(settings.AppName))
        {
            errors.Add("App name is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.AppDbConnectionString))
        {
            errors.Add("App DB connection string is required.");
        }
        else if (!settings.AppDbConnectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Connection string should contain 'Data Source='.");
        }

        var compliance = NormalizeComplianceSettings(settings.Compliance);
        if (compliance.CriticalDays <= 0)
        {
            errors.Add("Compliance critical days must be greater than 0.");
        }

        if (compliance.WarningDays <= compliance.CriticalDays)
        {
            errors.Add("Compliance warning days must be greater than critical days.");
        }

        var audit = NormalizeAuditSettings(settings.Audit);
        if (!IsAllowedRetentionDays(audit.RetentionDays))
        {
            errors.Add("Audit retention days must be one of 30, 90, 180, or 365.");
        }

        return Task.FromResult(new BootstrapSettingsValidationResult(errors.Count == 0, errors));
    }

    public async Task SaveAsync(BootstrapSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

        var document = new BootstrapSettingsDocument
        {
            AppName = string.IsNullOrWhiteSpace(settings.AppName) ? "License Watch" : settings.AppName,
            EnvironmentLabel = settings.EnvironmentLabel,
            Notes = settings.Notes,
            ProtectedAppDbConnectionString = Protect(settings.AppDbConnectionString),
            Branding = new BootstrapBrandingSettingsDocument
            {
                CompanyName = string.IsNullOrWhiteSpace(settings.Branding.CompanyName)
                    ? "LicenseWatch"
                    : settings.Branding.CompanyName,
                LogoFileName = settings.Branding.LogoFileName
            },
            Email = new BootstrapEmailSettingsDocument
            {
                SmtpHost = settings.Email.SmtpHost,
                SmtpPort = settings.Email.SmtpPort,
                UseSsl = settings.Email.UseSsl,
                IgnoreTlsErrors = settings.Email.IgnoreTlsErrors,
                EnableDailySummary = settings.Email.EnableDailySummary,
                Username = settings.Email.Username,
                ProtectedPassword = Protect(settings.Email.Password),
                FromName = settings.Email.FromName,
                FromEmail = settings.Email.FromEmail,
                DefaultToEmail = settings.Email.DefaultToEmail,
                SuppressionMinutes = settings.Email.SuppressionMinutes
            },
            Compliance = new BootstrapComplianceSettingsDocument
            {
                CriticalDays = settings.Compliance.CriticalDays,
                WarningDays = settings.Compliance.WarningDays
            },
            Audit = new BootstrapAuditSettingsDocument
            {
                RetentionDays = settings.Audit.RetentionDays
            },
            LastSavedUtc = settings.LastSavedUtc == default ? DateTime.UtcNow : settings.LastSavedUtc
        };

        var tempPath = Path.Combine(Path.GetDirectoryName(_filePath)!, $"bootstrap-{Guid.NewGuid():N}.tmp");
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
        }

        File.Move(tempPath, _filePath, overwrite: true);
        _logger.LogInformation("Bootstrap settings saved to {Path}", _filePath);
    }

    private string? Protect(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        return _protector.Protect(plaintext);
    }

    private string? Unprotect(string? protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect bootstrap secret, returning null.");
            return null;
        }
    }

    private static ComplianceSettings NormalizeComplianceSettings(BootstrapComplianceSettingsDocument document)
    {
        var critical = document.CriticalDays <= 0 ? 30 : document.CriticalDays;
        var warning = document.WarningDays <= 0 ? 90 : document.WarningDays;
        if (warning <= critical)
        {
            warning = critical + 1;
        }

        return new ComplianceSettings
        {
            CriticalDays = critical,
            WarningDays = warning
        };
    }

    private static ComplianceSettings NormalizeComplianceSettings(ComplianceSettings settings)
    {
        var critical = settings.CriticalDays <= 0 ? 30 : settings.CriticalDays;
        var warning = settings.WarningDays <= 0 ? 90 : settings.WarningDays;
        if (warning <= critical)
        {
            warning = critical + 1;
        }

        return new ComplianceSettings
        {
            CriticalDays = critical,
            WarningDays = warning
        };
    }

    private static AuditSettings NormalizeAuditSettings(BootstrapAuditSettingsDocument document)
    {
        var retention = IsAllowedRetentionDays(document.RetentionDays) ? document.RetentionDays : 180;
        return new AuditSettings
        {
            RetentionDays = retention
        };
    }

    private static AuditSettings NormalizeAuditSettings(AuditSettings settings)
    {
        var retention = IsAllowedRetentionDays(settings.RetentionDays) ? settings.RetentionDays : 180;
        return new AuditSettings
        {
            RetentionDays = retention
        };
    }

    private static bool IsAllowedRetentionDays(int retentionDays)
        => retentionDays is 30 or 90 or 180 or 365;
}
