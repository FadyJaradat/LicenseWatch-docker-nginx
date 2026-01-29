using System.Text;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.AuditView)]
[Route("admin/audit")]
public class AuditController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IPermissionService _permissionService;

    public AuditController(AppDbContext dbContext, IPermissionService permissionService)
    {
        _dbContext = dbContext;
        _permissionService = permissionService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? actionKey = null,
        string? entityType = null,
        string? correlationId = null,
        string? search = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int? rangeDays = null,
        int page = 1,
        string? tab = null)
    {
        var activeTab = string.IsNullOrWhiteSpace(tab) ? "audit" : tab;
        if (rangeDays.HasValue && !fromUtc.HasValue)
        {
            fromUtc = DateTime.UtcNow.AddDays(-rangeDays.Value);
        }

        if (rangeDays.HasValue && !toUtc.HasValue)
        {
            toUtc = DateTime.UtcNow;
        }

        var query = _dbContext.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(actionKey))
        {
            query = query.Where(l => l.Action == actionKey);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(l => l.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(l => l.CorrelationId != null && l.CorrelationId.Contains(correlationId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l => l.ActorEmail.Contains(search)
                                     || (l.ActorDisplay != null && l.ActorDisplay.Contains(search))
                                     || l.Summary.Contains(search)
                                     || (l.CorrelationId != null && l.CorrelationId.Contains(search)));
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(l => l.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(l => l.OccurredAtUtc <= toUtc.Value);
        }

        const int pageSize = 50;
        page = Math.Clamp(page, 1, 200);
        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var actionOptions = await _dbContext.AuditLogs.AsNoTracking()
            .Select(l => l.Action)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();
        var entityOptions = await _dbContext.AuditLogs.AsNoTracking()
            .Select(l => l.EntityType)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync();

        var canViewSensitive = User.IsInRole("SystemAdmin")
                               || await _permissionService.HasPermissionAsync(User, PermissionKeys.SecurityView);
        var systemLogs = await BuildSystemLogsAsync(canViewSensitive);

        var vm = new AuditListViewModel
        {
            Action = actionKey,
            EntityType = entityType,
            CorrelationId = correlationId,
            ActionOptions = actionOptions,
            EntityTypeOptions = entityOptions,
            Search = search,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            RangeDays = rangeDays,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            ActiveTab = activeTab,
            CanViewSensitiveDetails = canViewSensitive,
            Logs = logs.Select(l => MapAuditLog(l, canViewSensitive)).ToList(),
            SystemLogs = systemLogs
        };

        return View(vm);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        string? actionKey = null,
        string? entityType = null,
        string? correlationId = null,
        string? search = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int? rangeDays = null)
    {
        if (rangeDays.HasValue && !fromUtc.HasValue)
        {
            fromUtc = DateTime.UtcNow.AddDays(-rangeDays.Value);
        }

        if (rangeDays.HasValue && !toUtc.HasValue)
        {
            toUtc = DateTime.UtcNow;
        }

        var query = _dbContext.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(actionKey))
        {
            query = query.Where(l => l.Action == actionKey);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(l => l.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(l => l.CorrelationId != null && l.CorrelationId.Contains(correlationId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l => l.ActorEmail.Contains(search)
                                     || (l.ActorDisplay != null && l.ActorDisplay.Contains(search))
                                     || l.Summary.Contains(search)
                                     || (l.CorrelationId != null && l.CorrelationId.Contains(search)));
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(l => l.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(l => l.OccurredAtUtc <= toUtc.Value);
        }

        var logs = await query
            .OrderByDescending(l => l.OccurredAtUtc)
            .Take(1000)
            .ToListAsync();

        var canViewSensitive = User.IsInRole("SystemAdmin")
                               || await _permissionService.HasPermissionAsync(User, PermissionKeys.SecurityView);
        var bytes = BuildCsv(logs, canViewSensitive);
        var fileName = $"audit-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private static string ResolveSeverity(string action)
    {
        if (action.StartsWith("Security.", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        if (action.StartsWith("Compliance.", StringComparison.OrdinalIgnoreCase))
        {
            return "Critical";
        }

        if (action.StartsWith("Migrations.", StringComparison.OrdinalIgnoreCase))
        {
            return "Warning";
        }

        if (action.StartsWith("Maintenance.", StringComparison.OrdinalIgnoreCase))
        {
            return "Info";
        }

        if (action.Contains("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return "Critical";
        }

        return "Info";
    }

    private static string ResolveOutcome(AuditLog log)
    {
        if (log.Summary.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || log.Summary.Contains("error", StringComparison.OrdinalIgnoreCase)
            || log.Action.Contains("Failed", StringComparison.OrdinalIgnoreCase))
        {
            return "Failed";
        }

        return "Success";
    }

    private async Task<IReadOnlyCollection<SystemLogItemViewModel>> BuildSystemLogsAsync(bool canViewSensitive)
    {
        var jobLogs = await _dbContext.JobExecutionLogs.AsNoTracking()
            .OrderByDescending(l => l.StartedAtUtc)
            .Take(100)
            .ToListAsync();

        var emailLogs = await _dbContext.NotificationLogs.AsNoTracking()
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(100)
            .ToListAsync();

        var auditLogs = await _dbContext.AuditLogs.AsNoTracking()
            .Where(l => l.Action.StartsWith("Security.")
                        || l.Action.StartsWith("Maintenance.")
                        || l.Action.StartsWith("Migrations.")
                        || l.Action.StartsWith("Roles.")
                        || l.Action.StartsWith("Users.")
                        || l.Action.StartsWith("Settings.")
                        || l.Action.StartsWith("Database.")
                        || l.Action.StartsWith("System.")
                        || l.Action.StartsWith("Jobs.")
                        || l.Action.StartsWith("Email.")
                        || l.Action.StartsWith("Reports."))
            .OrderByDescending(l => l.OccurredAtUtc)
            .Take(100)
            .ToListAsync();

        var systemLogs = new List<SystemLogItemViewModel>();

        systemLogs.AddRange(jobLogs.Select(log => new SystemLogItemViewModel
        {
            OccurredAtUtc = log.StartedAtUtc,
            Source = "Jobs",
            Actor = "System",
            Summary = string.IsNullOrWhiteSpace(log.Summary) ? $"{log.JobKey} executed" : log.Summary,
            Status = log.Status,
            Detail = log.Error,
            DetailDisplay = canViewSensitive
                ? log.Error
                : (string.IsNullOrWhiteSpace(log.Error) ? null : "Redacted: requires security.view."),
            IsRedacted = !canViewSensitive && !string.IsNullOrWhiteSpace(log.Error),
            RedactionReason = !canViewSensitive && !string.IsNullOrWhiteSpace(log.Error) ? "Requires security.view" : null,
            EntityType = "JobExecution",
            EntityId = log.JobKey,
            CorrelationId = log.CorrelationId
        }));

        systemLogs.AddRange(emailLogs.Select(log => new SystemLogItemViewModel
        {
            OccurredAtUtc = log.CreatedAtUtc,
            Source = "Email",
            Actor = "System",
            Summary = $"{log.Type} to {log.ToEmail}",
            Status = log.Status,
            Detail = log.Error,
            DetailDisplay = canViewSensitive
                ? log.Error
                : (string.IsNullOrWhiteSpace(log.Error) ? null : "Redacted: requires security.view."),
            IsRedacted = !canViewSensitive && !string.IsNullOrWhiteSpace(log.Error),
            RedactionReason = !canViewSensitive && !string.IsNullOrWhiteSpace(log.Error) ? "Requires security.view" : null,
            EntityType = log.TriggerEntityType,
            EntityId = log.TriggerEntityId,
            CorrelationId = log.CorrelationId
        }));

        systemLogs.AddRange(auditLogs.Select(log => new SystemLogItemViewModel
        {
            OccurredAtUtc = log.OccurredAtUtc,
            Source = "Audit",
            Actor = ResolveActorDisplay(log),
            ActingAs = log.ImpersonatedDisplay,
            IsImpersonated = !string.IsNullOrWhiteSpace(log.ImpersonatedDisplay),
            Summary = log.Summary,
            Status = ResolveOutcome(log),
            Detail = log.Action,
            DetailDisplay = log.Action,
            IsRedacted = false,
            RedactionReason = null,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            CorrelationId = log.CorrelationId
        }));

        return systemLogs
            .OrderByDescending(l => l.OccurredAtUtc)
            .Take(200)
            .ToList();
    }

    private static byte[] BuildCsv(IReadOnlyList<AuditLog> logs, bool canViewSensitive)
    {
        var builder = new StringBuilder();
        builder.AppendLine("OccurredAtUtc,Actor,ActingAs,Action,EntityType,EntityId,Summary,IpAddress,CorrelationId");
        foreach (var log in logs)
        {
            var actor = ResolveActorDisplay(log);
            var actingAs = log.ImpersonatedDisplay;
            var entityId = canViewSensitive ? log.EntityId : "Redacted";
            var ipAddress = canViewSensitive ? log.IpAddress : "Redacted";
            builder.AppendLine(string.Join(",",
                Escape(log.OccurredAtUtc.ToString("o")),
                Escape(actor),
                Escape(actingAs),
                Escape(log.Action),
                Escape(log.EntityType),
                Escape(entityId),
                Escape(log.Summary),
                Escape(ipAddress),
                Escape(log.CorrelationId)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static AuditLogItemViewModel MapAuditLog(AuditLog log, bool canViewSensitive)
    {
        var actorDisplay = ResolveActorDisplay(log);
        var entityIdDisplay = canViewSensitive ? log.EntityId : "Redacted";
        var ipDisplay = canViewSensitive ? (string.IsNullOrWhiteSpace(log.IpAddress) ? "—" : log.IpAddress) : "Redacted";

        return new AuditLogItemViewModel
        {
            OccurredAtUtc = log.OccurredAtUtc,
            ActorEmail = log.ActorEmail,
            ActorDisplay = actorDisplay,
            ActingAs = log.ImpersonatedDisplay,
            IsImpersonated = !string.IsNullOrWhiteSpace(log.ImpersonatedDisplay),
            Action = log.Action,
            Summary = log.Summary,
            EntityType = log.EntityType,
            EntityId = log.EntityId,
            EntityIdDisplay = entityIdDisplay,
            IpAddress = log.IpAddress,
            IpAddressDisplay = ipDisplay,
            Severity = ResolveSeverity(log.Action),
            Outcome = ResolveOutcome(log),
            CorrelationId = log.CorrelationId
        };
    }

    private static string ResolveActorDisplay(AuditLog log)
    {
        if (!string.IsNullOrWhiteSpace(log.ActorDisplay))
        {
            return log.ActorDisplay!;
        }

        if (!string.IsNullOrWhiteSpace(log.ActorEmail))
        {
            return log.ActorEmail;
        }

        return "System";
    }
}
