using System.Net.Mail;
using System.Security.Claims;
using LicenseWatch.Core.Entities;
using LicenseWatch.Core.Models;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Email;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Identity;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.EmailView)]
[Route("admin/email")]
public class EmailController : Controller
{
    private static readonly string[] TemplateTokens =
    {
        "{{AppName}}", "{{AppVersion}}", "{{LicenseName}}", "{{Vendor}}", "{{ExpiresOn}}", "{{Severity}}", "{{DashboardUrl}}"
    };

    private readonly IBootstrapSettingsStore _store;
    private readonly IEmailSender _emailSender;
    private readonly IEmailTemplateRenderer _renderer;
    private readonly AppDbContext _dbContext;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IBootstrapSettingsStore store,
        IEmailSender emailSender,
        IEmailTemplateRenderer renderer,
        AppDbContext dbContext,
        RoleManager<IdentityRole> roleManager,
        IAuditLogger auditLogger,
        ILogger<EmailController> logger)
    {
        _store = store;
        _emailSender = emailSender;
        _renderer = renderer;
        _dbContext = dbContext;
        _roleManager = roleManager;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var settings = await _store.LoadAsync();
        var vm = BuildSettingsViewModel(settings);
        return View(vm);
    }

    [HttpPost("save")]
    [Authorize(Policy = PermissionPolicies.EmailManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EmailSettingsInputModel input)
    {
        var validationErrors = ValidateEmailInput(input);
        var settings = await _store.LoadAsync();
        var previousIgnoreTls = settings.Email.IgnoreTlsErrors;
        if (validationErrors.Count > 0)
        {
            var vmInvalid = BuildSettingsViewModel(settings);
            vmInvalid.Input = input;
            vmInvalid.AlertMessage = "Please correct the highlighted issues.";
            vmInvalid.AlertStyle = "danger";
            vmInvalid.AlertDetails = string.Join(Environment.NewLine, validationErrors);
            return View("Index", vmInvalid);
        }

        settings.Email = new EmailSettings
        {
            SmtpHost = input.SmtpHost.Trim(),
            SmtpPort = input.SmtpPort,
            UseSsl = input.UseSsl,
            IgnoreTlsErrors = input.IgnoreTlsErrors,
            EnableDailySummary = input.EnableDailySummary,
            Username = string.IsNullOrWhiteSpace(input.Username) ? null : input.Username.Trim(),
            Password = string.IsNullOrWhiteSpace(input.Password) ? settings.Email.Password : input.Password,
            FromName = input.FromName.Trim(),
            FromEmail = input.FromEmail.Trim(),
            DefaultToEmail = string.IsNullOrWhiteSpace(input.DefaultToEmail) ? null : input.DefaultToEmail.Trim(),
            SuppressionMinutes = input.SuppressionMinutes <= 0 ? 60 : input.SuppressionMinutes
        };
        settings.LastSavedUtc = DateTime.UtcNow;

        await _store.SaveAsync(settings);
        await LogAuditAsync("EmailSettings.Updated", "EmailSettings", settings.LastSavedUtc.ToString("O"), "Updated email settings.");
        if (previousIgnoreTls != settings.Email.IgnoreTlsErrors)
        {
            var summary = settings.Email.IgnoreTlsErrors
                ? "TLS certificate validation disabled for SMTP relay."
                : "TLS certificate validation re-enabled for SMTP relay.";
            await LogAuditAsync("EmailSettings.TlsValidationToggled", "EmailSettings", settings.LastSavedUtc.ToString("O"), summary);
        }

        var vm = BuildSettingsViewModel(settings);
        vm.AlertMessage = "Email settings saved.";
        vm.AlertStyle = "success";
        return View("Index", vm);
    }

    [HttpPost("send-test")]
    [Authorize(Policy = PermissionPolicies.EmailManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(string? toEmail)
    {
        var settings = await _store.LoadAsync();
        var target = string.IsNullOrWhiteSpace(toEmail)
            ? settings.Email.DefaultToEmail
            : toEmail.Trim();

        if (string.IsNullOrWhiteSpace(target))
        {
            var vmMissing = BuildSettingsViewModel(settings);
            vmMissing.AlertMessage = "Enter a recipient email or set a default.";
            vmMissing.AlertStyle = "warning";
            return View("Index", vmMissing);
        }

        if (!IsValidEmail(target))
        {
            var vmInvalid = BuildSettingsViewModel(settings);
            vmInvalid.AlertMessage = "Recipient email address is invalid.";
            vmInvalid.AlertStyle = "warning";
            return View("Index", vmInvalid);
        }

        var subject = $"{settings.AppName} Test Email";
        var htmlBody = $"<h2>{settings.AppName}</h2><p>This is a test email from LicenseWatch. If you received this, SMTP is configured.</p><p class=\"small\">{AppInfo.DisplayVersion}</p>";
        var textBody = $"{settings.AppName} test email. If you received this, SMTP is configured.{Environment.NewLine}Version: {AppInfo.DisplayVersion}";

        var result = await _emailSender.SendAsync(
            target,
            subject,
            htmlBody,
            textBody,
            "TestEmail",
            null,
            null,
            null,
            null);

        var message = result.Status switch
        {
            "Sent" => "Test email sent successfully.",
            "Suppressed" => "Test email suppressed to prevent repeated sends.",
            _ => "Test email failed. Check logs for details."
        };

        await LogAuditAsync("Email.TestSent", "NotificationLog", target, $"Test email attempted ({result.Status}) to {target}.");

        var vm = BuildSettingsViewModel(settings);
        vm.AlertMessage = message;
        vm.AlertStyle = result.Status == "Sent" ? "success" : result.Status == "Suppressed" ? "info" : "danger";
        if (result.Status == "Failed" && !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            vm.AlertDetails = result.ErrorMessage;
        }

        return View("Index", vm);
    }

    [HttpGet("templates")]
    public async Task<IActionResult> Templates()
    {
        var templates = await _dbContext.EmailTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        var vm = new EmailTemplateListViewModel
        {
            Templates = templates.Select(t => new EmailTemplateListItemViewModel
            {
                Id = t.Id,
                Key = t.Key,
                Name = t.Name,
                IsEnabled = t.IsEnabled,
                UpdatedAtUtc = t.UpdatedAtUtc
            }).ToList(),
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications()
    {
        var rules = await _dbContext.EmailNotificationRules.AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync();

        var roles = await _roleManager.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name ?? string.Empty)
            .ToListAsync();

        var vm = new EmailNotificationSettingsViewModel
        {
            Rules = rules.Select(rule => new EmailNotificationRuleInputModel
            {
                Id = rule.Id,
                EventKey = rule.EventKey,
                Name = rule.Name,
                Frequency = rule.Frequency,
                IsEnabled = rule.IsEnabled,
                SelectedRoles = (rule.RoleRecipients ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList()
            }).ToList(),
            Frequencies = EmailNotificationCatalog.Frequencies,
            Roles = roles,
            AlertMessage = TempData["EmailAlertMessage"] as string,
            AlertStyle = TempData["EmailAlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpPost("notifications")]
    [Authorize(Policy = PermissionPolicies.EmailManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotifications(EmailNotificationSettingsViewModel input)
    {
        if (input.Rules.Count == 0)
        {
            TempData["EmailAlertMessage"] = "No notification rules were submitted.";
            TempData["EmailAlertStyle"] = "warning";
            return RedirectToAction(nameof(Notifications));
        }

        var existing = await _dbContext.EmailNotificationRules.ToListAsync();
        var now = DateTime.UtcNow;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        foreach (var rule in input.Rules)
        {
            var entity = existing.FirstOrDefault(r => r.Id == rule.Id);
            if (entity is null)
            {
                continue;
            }

            entity.IsEnabled = rule.IsEnabled;
            entity.Frequency = EmailNotificationCatalog.Frequencies.Contains(rule.Frequency)
                ? rule.Frequency
                : "Instant";
            entity.RoleRecipients = rule.SelectedRoles.Count > 0
                ? string.Join(",", rule.SelectedRoles.Distinct(StringComparer.OrdinalIgnoreCase))
                : "SystemAdmin";
            entity.UpdatedAtUtc = now;
            entity.UpdatedByUserId = userId;
        }

        await _dbContext.SaveChangesAsync();

        await LogAuditAsync("EmailNotifications.Updated", "EmailNotificationRule", "Bulk", "Updated email notification rules.");

        TempData["EmailAlertMessage"] = "Notification rules updated.";
        TempData["EmailAlertStyle"] = "success";
        return RedirectToAction(nameof(Notifications));
    }

    [HttpPost("templates/{id:guid}/toggle")]
    [Authorize(Policy = PermissionPolicies.EmailManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTemplate(Guid id)
    {
        var template = await _dbContext.EmailTemplates.FindAsync(id);
        if (template is null)
        {
            TempData["AlertMessage"] = "Template not found.";
            TempData["AlertStyle"] = "warning";
            return RedirectToAction(nameof(Templates));
        }

        template.IsEnabled = !template.IsEnabled;
        template.UpdatedAtUtc = DateTime.UtcNow;
        template.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _dbContext.SaveChangesAsync();

        await LogAuditAsync("EmailTemplate.EnabledToggled", "EmailTemplate", template.Id.ToString(),
            $"Template {(template.IsEnabled ? "enabled" : "disabled")}: {template.Name}");

        TempData["AlertMessage"] = $"Template {(template.IsEnabled ? "enabled" : "disabled")}.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Templates));
    }

    [HttpGet("templates/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.EmailManage)]
    public async Task<IActionResult> EditTemplate(Guid id)
    {
        var template = await _dbContext.EmailTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (template is null)
        {
            return RedirectToAction(nameof(Templates));
        }

        var vm = await BuildTemplateEditViewModel(template);
        return View(vm);
    }

    [HttpPost("templates/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.EmailManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplate(Guid id, EmailTemplateEditViewModel input, string action)
    {
        var template = await _dbContext.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (template is null)
        {
            return RedirectToAction(nameof(Templates));
        }

        template.Name = input.Name.Trim();
        template.SubjectTemplate = input.SubjectTemplate.Trim();
        template.BodyHtmlTemplate = input.BodyHtmlTemplate.Trim();
        template.BodyTextTemplate = string.IsNullOrWhiteSpace(input.BodyTextTemplate) ? null : input.BodyTextTemplate.Trim();
        template.IsEnabled = input.IsEnabled;

        var preview = await BuildTemplateEditViewModel(template, input);
        if (string.Equals(action, "preview", StringComparison.OrdinalIgnoreCase))
        {
            preview.AlertMessage = "Preview updated.";
            preview.AlertStyle = "info";
            return View(preview);
        }

        template.UpdatedAtUtc = DateTime.UtcNow;
        template.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _dbContext.SaveChangesAsync();

        await LogAuditAsync("EmailTemplate.Updated", "EmailTemplate", template.Id.ToString(), $"Updated template {template.Name}.");

        preview.AlertMessage = "Template saved.";
        preview.AlertStyle = "success";
        return View(preview);
    }

    [HttpGet("log")]
    public async Task<IActionResult> Log(string? status = null, string? type = null, string? search = null, DateTime? fromUtc = null, DateTime? toUtc = null)
    {
        var types = await _dbContext.NotificationLogs.AsNoTracking()
            .Select(l => l.Type)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

        var query = _dbContext.NotificationLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(l => l.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l => l.ToEmail.Contains(search) || l.Subject.Contains(search));
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(l => l.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(l => l.CreatedAtUtc <= toUtc.Value);
        }

        var logs = await query.OrderByDescending(l => l.CreatedAtUtc).Take(200).ToListAsync();

        var vm = new NotificationLogListViewModel
        {
            Status = status,
            Type = type,
            Search = search,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Types = types,
            Items = logs.Select(l => new NotificationLogItemViewModel
            {
                Id = l.Id,
                CreatedAtUtc = l.CreatedAtUtc,
                Type = l.Type,
                ToEmail = l.ToEmail,
                Subject = l.Subject,
                Status = l.Status
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet("log/{id:guid}")]
    public async Task<IActionResult> LogDetails(Guid id)
    {
        var log = await _dbContext.NotificationLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (log is null)
        {
            return RedirectToAction(nameof(Log));
        }

        var vm = new NotificationLogDetailViewModel
        {
            Id = log.Id,
            CreatedAtUtc = log.CreatedAtUtc,
            Type = log.Type,
            ToEmail = log.ToEmail,
            Subject = log.Subject,
            Status = log.Status,
            Error = log.Error,
            CorrelationId = log.CorrelationId,
            TriggerEntityType = log.TriggerEntityType,
            TriggerEntityId = log.TriggerEntityId
        };

        return View(vm);
    }

    private EmailSettingsViewModel BuildSettingsViewModel(BootstrapSettings settings)
    {
        return new EmailSettingsViewModel
        {
            Input = new EmailSettingsInputModel
            {
                SmtpHost = settings.Email.SmtpHost,
                SmtpPort = settings.Email.SmtpPort,
                UseSsl = settings.Email.UseSsl,
                IgnoreTlsErrors = settings.Email.IgnoreTlsErrors,
                EnableDailySummary = settings.Email.EnableDailySummary,
                Username = settings.Email.Username,
                Password = string.Empty,
                FromName = settings.Email.FromName,
                FromEmail = settings.Email.FromEmail,
                DefaultToEmail = settings.Email.DefaultToEmail,
                SuppressionMinutes = settings.Email.SuppressionMinutes
            },
            HasPassword = !string.IsNullOrWhiteSpace(settings.Email.Password)
        };
    }

    private async Task<EmailTemplateEditViewModel> BuildTemplateEditViewModel(EmailTemplate template, EmailTemplateEditViewModel? input = null)
    {
        var tokens = BuildSampleTokens(await _store.LoadAsync());
        var render = _renderer.Render(template, tokens);

        return new EmailTemplateEditViewModel
        {
            Id = template.Id,
            Key = template.Key,
            Name = input?.Name ?? template.Name,
            SubjectTemplate = input?.SubjectTemplate ?? template.SubjectTemplate,
            BodyHtmlTemplate = input?.BodyHtmlTemplate ?? template.BodyHtmlTemplate,
            BodyTextTemplate = input?.BodyTextTemplate ?? template.BodyTextTemplate,
            IsEnabled = input?.IsEnabled ?? template.IsEnabled,
            PreviewSubject = render.Subject,
            PreviewHtml = render.HtmlBody,
            PreviewText = render.TextBody,
            AvailableTokens = TemplateTokens
        };
    }

    private IReadOnlyDictionary<string, string?> BuildSampleTokens(BootstrapSettings settings)
    {
        var dashboardUrl = $"{Request.Scheme}://{Request.Host}/admin";
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppName"] = settings.AppName,
            ["AppVersion"] = AppInfo.DisplayVersion,
            ["LicenseName"] = "Contoso Suite",
            ["Vendor"] = "Contoso",
            ["ExpiresOn"] = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
            ["Severity"] = "Critical",
            ["DashboardUrl"] = dashboardUrl
        };
    }

    private async Task LogAuditAsync(string action, string entityType, string entityId, string summary)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email = User.Identity?.Name ?? string.Empty;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = userId,
            ActorEmail = email,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            IpAddress = ip
        });
    }

    private static List<string> ValidateEmailInput(EmailSettingsInputModel input)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.SmtpHost))
        {
            errors.Add("SMTP host is required.");
        }

        if (input.SmtpPort <= 0)
        {
            errors.Add("SMTP port must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(input.FromName))
        {
            errors.Add("From name is required.");
        }

        if (string.IsNullOrWhiteSpace(input.FromEmail))
        {
            errors.Add("From email is required.");
        }
        else if (!IsValidEmail(input.FromEmail))
        {
            errors.Add("From email is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(input.DefaultToEmail) && !IsValidEmail(input.DefaultToEmail))
        {
            errors.Add("Default test recipient email is invalid.");
        }

        return errors;
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
