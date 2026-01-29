using System.Net.Mail;
using System.Security.Claims;
using LicenseWatch.Core.Diagnostics;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Infrastructure.Email;

public interface IEmailNotificationService
{
    Task<EmailNotificationResult> NotifyAsync(string eventKey, EmailNotificationContext context, CancellationToken cancellationToken = default);
    Task<string> RunDigestAsync(CancellationToken cancellationToken = default);
}

public record EmailNotificationResult(string Status, int Recipients, string? Message = null);

public record EmailNotificationContext(
    Guid? EntityId,
    string? EntityType,
    string? Title,
    string? Summary,
    string? Vendor,
    DateTime? ExpiresOnUtc,
    string? Severity,
    int? Count,
    string? DashboardUrl,
    string? ActorEmail);

public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly AppDbContext _dbContext;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IBootstrapSettingsStore _settingsStore;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        AppDbContext dbContext,
        UserManager<IdentityUser> userManager,
        IEmailSender emailSender,
        IBootstrapSettingsStore settingsStore,
        IAuditLogger auditLogger,
        ILogger<EmailNotificationService> logger)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _emailSender = emailSender;
        _settingsStore = settingsStore;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<EmailNotificationResult> NotifyAsync(string eventKey, EmailNotificationContext context, CancellationToken cancellationToken = default)
    {
        var rule = await _dbContext.EmailNotificationRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EventKey == eventKey, cancellationToken);

        if (rule is null || !rule.IsEnabled)
        {
            return new EmailNotificationResult("Skipped", 0, "Rule disabled or missing.");
        }

        if (!string.Equals(rule.Frequency, "Instant", StringComparison.OrdinalIgnoreCase))
        {
            return new EmailNotificationResult("Deferred", 0, "Rule configured for digest.");
        }

        var recipients = await ResolveRecipientsAsync(rule.RoleRecipients, cancellationToken);
        if (recipients.Count == 0)
        {
            return new EmailNotificationResult("Skipped", 0, "No recipients configured.");
        }

        var settings = await _settingsStore.LoadAsync(cancellationToken);
        var subject = BuildSubject(settings.AppName, rule.Name, context.Title);
        var dashboardUrl = context.DashboardUrl ?? "/admin";
        var htmlBody = BuildHtml(settings.AppName, rule.Name, context, dashboardUrl);
        var textBody = BuildText(settings.AppName, rule.Name, context, dashboardUrl);

        var sent = 0;
        var correlationId = CorrelationContext.Current;
        foreach (var recipient in recipients)
        {
            var result = await _emailSender.SendAsync(
                recipient,
                subject,
                htmlBody,
                textBody,
                $"Notification:{eventKey}",
                correlationId,
                context.EntityType,
                context.EntityId?.ToString(),
                null,
                cancellationToken);

            if (result.Status == "Sent")
            {
                sent++;
            }
        }

        await LogAuditAsync(rule, context, sent, cancellationToken);
        return new EmailNotificationResult("Sent", sent);
    }

    public async Task<string> RunDigestAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _dbContext.EmailNotificationRules.AsNoTracking()
            .Where(r => r.IsEnabled && (r.Frequency == "Daily" || r.Frequency == "Weekly"))
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return "No digest rules are enabled.";
        }

        var settings = await _settingsStore.LoadAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var summaries = new List<string>();

        foreach (var rule in rules)
        {
            if (string.Equals(rule.Frequency, "Weekly", StringComparison.OrdinalIgnoreCase)
                && now.DayOfWeek != DayOfWeek.Monday)
            {
                continue;
            }

            var window = string.Equals(rule.Frequency, "Weekly", StringComparison.OrdinalIgnoreCase)
                ? TimeSpan.FromDays(7)
                : TimeSpan.FromDays(1);

            var windowStart = now.Subtract(window);
            var count = await CountEventsAsync(rule.EventKey, windowStart, now, cancellationToken);
            var recipients = await ResolveRecipientsAsync(rule.RoleRecipients, cancellationToken);
            if (recipients.Count == 0)
            {
                summaries.Add($"{rule.Name}: no recipients.");
                continue;
            }

            var subject = $"{settings.AppName} digest · {rule.Name}";
            var htmlBody = $"<h2>{settings.AppName}</h2><p>{rule.Name}</p><p><strong>{count}</strong> events in the last {(int)window.TotalDays} days.</p><p>Review details in the admin console: /admin/audit</p><p class=\"small\">{BuildInfo.DisplayVersion}</p>";
            var textBody = $"{settings.AppName} {rule.Name}: {count} events in last {(int)window.TotalDays} days. Review /admin/audit{Environment.NewLine}Version: {BuildInfo.DisplayVersion}";

            var sent = 0;
            var correlationId = CorrelationContext.Current;
            foreach (var recipient in recipients)
            {
                var result = await _emailSender.SendAsync(
                    recipient,
                    subject,
                    htmlBody,
                    textBody,
                    $"Digest:{rule.EventKey}",
                    correlationId,
                    "Audit",
                    rule.EventKey,
                    null,
                    cancellationToken);

                if (result.Status == "Sent")
                {
                    sent++;
                }
            }

            await LogDigestAuditAsync(rule, count, sent, cancellationToken);
            summaries.Add($"{rule.Name}: {count} events, {sent}/{recipients.Count} sent.");
        }

        return summaries.Count == 0 ? "No digest emails sent." : string.Join(" ", summaries);
    }

    private async Task<IReadOnlyList<string>> ResolveRecipientsAsync(string? roleRecipients, CancellationToken cancellationToken)
    {
        var roles = ParseRoles(roleRecipients);
        if (roles.Count == 0)
        {
            roles.Add("SystemAdmin");
        }

        var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            foreach (var user in users)
            {
                if (!string.IsNullOrWhiteSpace(user.Email) && IsValidEmail(user.Email))
                {
                    recipients.Add(user.Email);
                }
            }
        }

        return recipients.ToList();
    }

    private static List<string> ParseRoles(string? roles)
    {
        if (string.IsNullOrWhiteSpace(roles))
        {
            return new List<string>();
        }

        return roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<int> CountEventsAsync(string eventKey, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
    {
        var actions = eventKey switch
        {
            "License.Created" => new[] { "License.Created" },
            "License.Updated" => new[] { "License.Updated" },
            "License.Deleted" => new[] { "License.Deleted" },
            "Compliance.Changes" => new[] { "Compliance.Evaluated", "Compliance.Acknowledged", "Compliance.Resolved" },
            "Optimization.Insights" => new[] { "Optimization.AnalysisRan", "Recommendation.Created", "Recommendation.Updated" },
            _ => Array.Empty<string>()
        };

        if (actions.Length == 0)
        {
            return 0;
        }

        return await _dbContext.AuditLogs.AsNoTracking()
            .Where(a => actions.Contains(a.Action) && a.OccurredAtUtc >= fromUtc && a.OccurredAtUtc <= toUtc)
            .CountAsync(cancellationToken);
    }

    private async Task LogAuditAsync(EmailNotificationRule rule, EmailNotificationContext context, int recipients, CancellationToken cancellationToken)
    {
        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = context.ActorEmail ?? "system",
            ActorEmail = context.ActorEmail ?? "system",
            Action = "EmailNotification.Sent",
            EntityType = "EmailNotificationRule",
            EntityId = rule.Id.ToString(),
            Summary = $"{rule.Name} sent to {recipients} recipients.",
            IpAddress = null
        }, cancellationToken);
    }

    private async Task LogDigestAuditAsync(EmailNotificationRule rule, int count, int recipients, CancellationToken cancellationToken)
    {
        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = "system",
            ActorEmail = "system",
            Action = "EmailNotification.DigestSent",
            EntityType = "EmailNotificationRule",
            EntityId = rule.Id.ToString(),
            Summary = $"{rule.Name} digest sent. Events: {count}, Recipients: {recipients}.",
            IpAddress = null
        }, cancellationToken);
    }

    private static string BuildSubject(string appName, string ruleName, string? title)
        => string.IsNullOrWhiteSpace(title)
            ? $"{appName} alert · {ruleName}"
            : $"{appName} alert · {title}";

    private static string BuildHtml(string appName, string ruleName, EmailNotificationContext context, string dashboardUrl)
    {
        var details = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(context.Summary))
        {
            details["Summary"] = context.Summary;
        }

        if (!string.IsNullOrWhiteSpace(context.Vendor))
        {
            details["Vendor"] = context.Vendor;
        }

        if (context.ExpiresOnUtc.HasValue)
        {
            details["Expires"] = context.ExpiresOnUtc.Value.ToString("yyyy-MM-dd");
        }

        if (!string.IsNullOrWhiteSpace(context.Severity))
        {
            details["Severity"] = context.Severity;
        }

        if (context.Count.HasValue)
        {
            details["Count"] = context.Count.Value.ToString();
        }

        var detailsHtml = details.Count == 0
            ? "<p>No additional details were provided.</p>"
            : "<ul>" + string.Join("", details.Select(item => $"<li><strong>{item.Key}:</strong> {item.Value}</li>")) + "</ul>";

        return $"<h2>{appName}</h2><p>{ruleName}</p>{detailsHtml}<p><a href=\"{dashboardUrl}\">Open dashboard</a></p><p class=\"small\">{BuildInfo.DisplayVersion}</p>";
    }

    private static string BuildText(string appName, string ruleName, EmailNotificationContext context, string dashboardUrl)
    {
        var lines = new List<string> { appName, ruleName };
        if (!string.IsNullOrWhiteSpace(context.Summary))
        {
            lines.Add($"Summary: {context.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(context.Vendor))
        {
            lines.Add($"Vendor: {context.Vendor}");
        }

        if (context.ExpiresOnUtc.HasValue)
        {
            lines.Add($"Expires: {context.ExpiresOnUtc.Value:yyyy-MM-dd}");
        }

        if (!string.IsNullOrWhiteSpace(context.Severity))
        {
            lines.Add($"Severity: {context.Severity}");
        }

        if (context.Count.HasValue)
        {
            lines.Add($"Count: {context.Count.Value}");
        }

        lines.Add($"Dashboard: {dashboardUrl}");
        lines.Add($"Version: {BuildInfo.DisplayVersion}");
        return string.Join(Environment.NewLine, lines);
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
