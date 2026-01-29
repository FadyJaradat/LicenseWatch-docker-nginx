using System.Security.Claims;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
using LicenseWatch.Infrastructure.Optimization;
using LicenseWatch.Infrastructure.Persistence;
using LicenseWatch.Web.Helpers;
using LicenseWatch.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LicenseWatch.Web.Security;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.OptimizationView)]
[Route("admin/optimization")]
public class OptimizationController : Controller
{
    private readonly AppDbContext _dbContext;
    private readonly IOptimizationEngine _engine;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<OptimizationController> _logger;

    public OptimizationController(
        AppDbContext dbContext,
        IOptimizationEngine engine,
        IAuditLogger auditLogger,
        ILogger<OptimizationController> logger)
    {
        _dbContext = dbContext;
        _engine = engine;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? severity = null, string? key = null, Guid? categoryId = null, string? status = null)
    {
        var categories = await _dbContext.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        var query = _dbContext.OptimizationInsights
            .Include(i => i.License)
            .Include(i => i.Category)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(severity))
        {
            query = query.Where(i => i.Severity == severity);
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            query = query.Where(i => i.Key == key);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(i => i.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.IsActive);
            }
            else if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => !i.IsActive);
            }
        }

        var insights = await query.ToListAsync();
        var ordered = insights
            .OrderByDescending(i => i.IsActive)
            .ThenBy(i => SeverityRank(i.Severity))
            .ThenByDescending(i => i.DetectedAtUtc)
            .Take(200)
            .Select(i => new OptimizationInsightItemViewModel
            {
                Id = i.Id,
                Key = i.Key,
                Title = i.Title,
                Severity = i.Severity,
                IsActive = i.IsActive,
                LicenseId = i.LicenseId,
                LicenseName = i.License?.Name ?? "Unknown license",
                CategoryId = i.CategoryId,
                CategoryName = i.Category?.Name,
                DetectedAtUtc = i.DetectedAtUtc,
                EvidenceSummary = OptimizationEvidenceFormatter.BuildSummary(i.Key, i.EvidenceJson)
            })
            .ToList();

        var activeCount = await _dbContext.OptimizationInsights.CountAsync(i => i.IsActive);
        var criticalCount = await _dbContext.OptimizationInsights.CountAsync(i => i.IsActive && i.Severity == "Critical");
        var recommendationsOpen = await _dbContext.Recommendations.CountAsync(r => r.Status == "Open" || r.Status == "InProgress");
        var estimatedAnnual = await _dbContext.Recommendations
            .Where(r => r.Status == "Open" || r.Status == "InProgress")
            .Select(r => r.EstimatedAnnualSavings ?? (r.EstimatedMonthlySavings.HasValue ? r.EstimatedMonthlySavings.Value * 12 : 0m))
            .SumAsync();

        var vm = new OptimizationOverviewViewModel
        {
            Insights = ordered,
            Categories = categories.Select(c => new CategoryOption { Id = c.Id, Name = c.Name }).ToList(),
            Severity = severity,
            Key = key,
            CategoryId = categoryId,
            Status = status,
            ActiveInsights = activeCount,
            CriticalInsights = criticalCount,
            EstimatedAnnualSavings = estimatedAnnual > 0 ? estimatedAnnual : null,
            RecommendationsOpen = recommendationsOpen,
            LastRefreshedUtc = DateTime.UtcNow,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info",
            AlertDetails = TempData["AlertDetails"] as string
        };

        return View(vm);
    }

    [HttpPost("run")]
    [Authorize(Policy = PermissionPolicies.OptimizationManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(int windowDays = 30, CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            windowDays = 30;
        }

        try
        {
            var result = await _engine.GenerateInsightsAsync(windowDays, cancellationToken);
            await LogAuditAsync(
                "Optimization.AnalysisRan",
                "Optimization",
                $"{DateTime.UtcNow:yyyyMMddHHmmss}",
                $"Optimization analysis ran: {result.Created} new, {result.Updated} updated, {result.Deactivated} deactivated.");

            SetTempAlert("Optimization analysis completed.", "success");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Optimization analysis failed.");
            SetTempAlert("Optimization analysis failed.", "danger", ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/dismiss")]
    [Authorize(Policy = PermissionPolicies.OptimizationManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(Guid id, string? note = null)
    {
        var insight = await _dbContext.OptimizationInsights
            .Include(i => i.License)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (insight is null)
        {
            SetTempAlert("Insight not found.", "warning");
            return RedirectToAction(nameof(Index));
        }

        if (!insight.IsActive)
        {
            SetTempAlert("Insight already dismissed.", "info");
            return RedirectToAction(nameof(Index));
        }

        insight.IsActive = false;
        await _dbContext.SaveChangesAsync();

        var summary = $"Dismissed insight: {insight.Title}.";
        if (!string.IsNullOrWhiteSpace(insight.License?.Name))
        {
            summary = $"{summary} License: {insight.License.Name}.";
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            summary = $"{summary} Note: {TrimToLength(note, 200)}";
        }

        await LogAuditAsync("Insight.Dismissed", "OptimizationInsight", insight.Id.ToString(), TrimToLength(summary, 500));
        SetTempAlert("Insight dismissed.", "success");

        return RedirectToAction(nameof(Index));
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

    private static int SeverityRank(string? severity)
        => severity switch
        {
            "Critical" => 0,
            "Warning" => 1,
            "Info" => 2,
            _ => 3
        };

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
