using System.Security.Claims;
using System.Text.Json;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Compliance;
using LicenseWatch.Infrastructure.Email;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseWatch.Web.Security;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.ComplianceView)]
[Route("admin/compliance")]
public class ComplianceController : Controller
{
    private static readonly Dictionary<string, string> EvidenceLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["seatsPurchased"] = "Seats purchased",
        ["peakUsed"] = "Peak used",
        ["dateOfPeak"] = "Peak date",
        ["windowDays"] = "Window",
        ["source"] = "Source",
        ["expiresOn"] = "Expires on",
        ["daysPastDue"] = "Days past due"
    };

    private readonly AppDbContext _dbContext;
    private readonly IComplianceEvaluator _evaluator;
    private readonly IAuditLogger _auditLogger;
    private readonly IEmailNotificationService _notifications;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(
        AppDbContext dbContext,
        IComplianceEvaluator evaluator,
        IAuditLogger auditLogger,
        IEmailNotificationService notifications,
        ILogger<ComplianceController> logger)
    {
        _dbContext = dbContext;
        _evaluator = evaluator;
        _auditLogger = auditLogger;
        _notifications = notifications;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status = null, string? severity = null, string? rule = null, string? search = null)
    {
        var openCount = await _dbContext.ComplianceViolations.CountAsync(v => v.Status == "Open");
        var criticalCount = await _dbContext.ComplianceViolations.CountAsync(v => v.Status == "Open" && v.Severity == "Critical");
        var warningCount = await _dbContext.ComplianceViolations.CountAsync(v => v.Status == "Open" && v.Severity == "Warning");
        var acknowledgedCount = await _dbContext.ComplianceViolations.CountAsync(v => v.Status == "Acknowledged");
        var lastEvaluatedAt = await _dbContext.ComplianceViolations.AsNoTracking()
            .MaxAsync(v => (DateTime?)v.LastEvaluatedAtUtc);

        var rules = await _dbContext.ComplianceViolations.AsNoTracking()
            .Select(v => v.RuleKey)
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync();

        var query = _dbContext.ComplianceViolations
            .Include(v => v.License)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(v => v.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(v => v.Severity == severity);
        }

        if (!string.IsNullOrWhiteSpace(rule))
        {
            query = query.Where(v => v.RuleKey == rule);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(v =>
                v.Title.Contains(search) ||
                v.Details.Contains(search) ||
                (v.License != null && (v.License.Name.Contains(search) || (v.License.Vendor != null && v.License.Vendor.Contains(search)))));
        }

        var violations = await query
            .OrderByDescending(v => v.DetectedAtUtc)
            .ThenByDescending(v => v.Severity)
            .Take(200)
            .ToListAsync();

        var vm = new ComplianceListViewModel
        {
            OpenCount = openCount,
            CriticalCount = criticalCount,
            WarningCount = warningCount,
            AcknowledgedCount = acknowledgedCount,
            LastEvaluatedAtUtc = lastEvaluatedAt,
            Status = status,
            Severity = severity,
            RuleKey = rule,
            Search = search,
            RuleKeys = rules,
            Violations = violations.Select(v => new ComplianceViolationViewModel
            {
                Id = v.Id,
                LicenseId = v.LicenseId,
                LicenseName = v.License?.Name ?? "Unknown license",
                Vendor = v.License?.Vendor,
                RuleKey = v.RuleKey,
                Severity = v.Severity,
                Status = v.Status,
                Title = v.Title,
                Details = v.Details,
                DetectedAtUtc = v.DetectedAtUtc,
                LastEvaluatedAtUtc = v.LastEvaluatedAtUtc,
                EvidenceItems = BuildEvidenceItems(v.EvidenceJson),
                CanSimulate = v.RuleKey == "Overuse" && v.Status != "Resolved"
            }).ToList(),
            AlertMessage = TempData["AlertMessage"] as string,
            AlertDetails = TempData["AlertDetails"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpPost("run")]
    [Authorize(Policy = PermissionPolicies.ComplianceManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(int? windowDays = null, DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken cancellationToken = default)
    {
        var (from, to) = ResolveWindow(windowDays, fromUtc, toUtc);
        var result = await _evaluator.EvaluateAsync(from, to, cancellationToken);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email = User.Identity?.Name ?? string.Empty;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = userId,
            ActorEmail = email,
            Action = "Compliance.Evaluated",
            EntityType = "Compliance",
            EntityId = $"{result.WindowStart:yyyyMMdd}-{result.WindowEnd:yyyyMMdd}",
            Summary = $"Compliance evaluation ran: {result.Opened} opened, {result.Resolved} resolved, {result.TotalOpen} open.",
            IpAddress = ip
        }, cancellationToken);

        await _notifications.NotifyAsync("Compliance.Changes", new EmailNotificationContext(
            null,
            "Compliance",
            "Compliance evaluation completed",
            $"Compliance evaluation completed. Opened {result.Opened}, Resolved {result.Resolved}, Updated {result.Updated}.",
            null,
            null,
            result.Opened > 0 ? "Warning" : "Info",
            result.Opened,
            $"{Request.Scheme}://{Request.Host}/admin/compliance",
            User.Identity?.Name), cancellationToken);

        var vm = new ComplianceRunResultViewModel
        {
            WindowStart = result.WindowStart,
            WindowEnd = result.WindowEnd,
            Opened = result.Opened,
            Resolved = result.Resolved,
            Updated = result.Updated,
            TotalOpen = result.TotalOpen,
            TotalAcknowledged = result.TotalAcknowledged,
            TotalResolved = result.TotalResolved
        };

        return View("Run", vm);
    }

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(Policy = PermissionPolicies.ComplianceManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Acknowledge(Guid id, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            SetTempAlert("Please add a note before acknowledging.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var violation = await _dbContext.ComplianceViolations.FindAsync(id);
        if (violation is null)
        {
            SetTempAlert("Violation not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        if (violation.Status == "Resolved")
        {
            SetTempAlert("Resolved violations cannot be acknowledged.", "warning");
            return RedirectToAction(nameof(Index));
        }

        violation.Status = "Acknowledged";
        violation.AcknowledgedAtUtc = DateTime.UtcNow;
        violation.AcknowledgedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        violation.LastEvaluatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("Compliance.Acknowledged", violation, note);
        SetTempAlert("Violation acknowledged.", "success");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = PermissionPolicies.ComplianceManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(Guid id, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            SetTempAlert("Please add a note before resolving.", "warning");
            return RedirectToAction(nameof(Index));
        }

        var violation = await _dbContext.ComplianceViolations.FindAsync(id);
        if (violation is null)
        {
            SetTempAlert("Violation not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        violation.Status = "Resolved";
        violation.ResolvedAtUtc = DateTime.UtcNow;
        violation.LastEvaluatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("Compliance.Resolved", violation, note);
        SetTempAlert("Violation marked as resolved.", "success");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/simulate")]
    [Authorize(Policy = PermissionPolicies.ComplianceManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulate(Guid id, string? note = null)
    {
        var violation = await _dbContext.ComplianceViolations.Include(v => v.License).FirstOrDefaultAsync(v => v.Id == id);
        if (violation is null)
        {
            SetTempAlert("Violation not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        if (!string.Equals(violation.RuleKey, "Overuse", StringComparison.OrdinalIgnoreCase))
        {
            SetTempAlert("Enforcement simulation is only available for overuse violations.", "warning");
            return RedirectToAction(nameof(Index));
        }

        await LogAuditAsync("Enforcement.Simulated", violation, note);
        SetTempAlert("Simulation logged. No enforcement actions were executed.", "info");
        return RedirectToAction(nameof(Index));
    }

    private async Task LogAuditAsync(string action, ComplianceViolation violation, string? note)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var email = User.Identity?.Name ?? string.Empty;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var summary = $"{action} for {violation.Title}.";
        if (!string.IsNullOrWhiteSpace(note))
        {
            summary = $"{summary} Note: {TrimToLength(note, 200)}";
        }

        await _auditLogger.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            ActorUserId = userId,
            ActorEmail = email,
            Action = action,
            EntityType = "ComplianceViolation",
            EntityId = violation.Id.ToString(),
            Summary = TrimToLength(summary, 500),
            IpAddress = ip
        });
    }

    private static IReadOnlyList<ComplianceEvidenceItemViewModel> BuildEvidenceItems(string? evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson))
        {
            return Array.Empty<ComplianceEvidenceItemViewModel>();
        }

        try
        {
            using var doc = JsonDocument.Parse(evidenceJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<ComplianceEvidenceItemViewModel>();
            }

            var items = new List<ComplianceEvidenceItemViewModel>();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var label = EvidenceLabels.TryGetValue(property.Name, out var mappedLabel)
                    ? mappedLabel
                    : property.Name;
                var value = FormatEvidenceValue(property.Name, property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    items.Add(new ComplianceEvidenceItemViewModel
                    {
                        Label = label,
                        Value = value
                    });
                }
            }

            return items;
        }
        catch
        {
            return Array.Empty<ComplianceEvidenceItemViewModel>();
        }
    }

    private static string FormatEvidenceValue(string key, JsonElement value)
    {
        return key switch
        {
            "windowDays" when value.ValueKind == JsonValueKind.Number => $"{value.GetInt32()} days",
            "source" => value.GetString() switch
            {
                "UsageDailySummary" => "Usage summaries",
                "SeatsAssigned" => "Seats assigned",
                _ => value.ToString()
            },
            _ => value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "Yes",
                JsonValueKind.False => "No",
                _ => value.ToString()
            }
        };
    }

    private static (DateOnly? From, DateOnly? To) ResolveWindow(int? windowDays, DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue || toUtc.HasValue)
        {
            var fromDate = fromUtc.HasValue ? DateOnly.FromDateTime(fromUtc.Value.Date) : (DateOnly?)null;
            var toDate = toUtc.HasValue ? DateOnly.FromDateTime(toUtc.Value.Date) : DateOnly.FromDateTime(DateTime.UtcNow.Date);
            return (fromDate, toDate);
        }

        if (windowDays.HasValue && windowDays.Value > 0)
        {
            var end = DateOnly.FromDateTime(DateTime.UtcNow.Date);
            return (end.AddDays(-(windowDays.Value - 1)), end);
        }

        return (null, null);
    }

    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private void SetTempAlert(string message, string style, string? details = null)
    {
        TempData["AlertMessage"] = message;
        TempData["AlertStyle"] = style;
        if (!string.IsNullOrWhiteSpace(details))
        {
            TempData["AlertDetails"] = details;
        }
    }
}
