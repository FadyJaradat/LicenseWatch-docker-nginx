using System.Security.Claims;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Auditing;
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
[Route("admin/optimization/recommendations")]
public class RecommendationsController : Controller
{
    private static readonly IReadOnlyList<string> StatusOptions = new[] { "Open", "InProgress", "Done", "Dismissed" };

    private readonly AppDbContext _dbContext;
    private readonly IAuditLogger _auditLogger;

    public RecommendationsController(AppDbContext dbContext, IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _auditLogger = auditLogger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status = null, Guid? categoryId = null, Guid? licenseId = null)
    {
        var query = _dbContext.Recommendations
            .Include(r => r.License)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (licenseId.HasValue)
        {
            query = query.Where(r => r.LicenseId == licenseId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(r => r.License != null && r.License.CategoryId == categoryId.Value);
        }

        var recommendations = await query
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Take(200)
            .ToListAsync();

        var vm = new RecommendationListViewModel
        {
            Recommendations = recommendations.Select(r => new RecommendationListItemViewModel
            {
                Id = r.Id,
                Title = r.Title,
                Status = r.Status,
                EstimatedAnnualSavings = r.EstimatedAnnualSavings ?? (r.EstimatedMonthlySavings.HasValue ? r.EstimatedMonthlySavings.Value * 12 : null),
                Currency = r.Currency,
                LicenseId = r.LicenseId,
                LicenseName = r.License?.Name ?? "Unassigned",
                UpdatedAtUtc = r.UpdatedAtUtc
            }).ToList(),
            Categories = await LoadCategoryOptions(),
            Licenses = await LoadLicenseOptions(),
            Status = status,
            CategoryId = categoryId,
            LicenseId = licenseId,
            StatusOptions = StatusOptions,
            LastRefreshedUtc = DateTime.UtcNow,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("create")]
    [Authorize(Policy = PermissionPolicies.OptimizationManage)]
    public async Task<IActionResult> Create(Guid? insightId = null)
    {
        var vm = new RecommendationFormViewModel
        {
            Licenses = await LoadLicenseOptions(),
            StatusOptions = StatusOptions,
            Currency = "USD"
        };

        if (insightId.HasValue)
        {
            await PopulateFromInsightAsync(vm, insightId.Value);
        }

        return View(vm);
    }

    [HttpPost("create")]
    [Authorize(Policy = PermissionPolicies.OptimizationManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RecommendationFormViewModel model)
    {
        NormalizeSavings(model);

        if (!ValidateModel(model))
        {
            model.Licenses = await LoadLicenseOptions();
            model.StatusOptions = StatusOptions;
            return View(model);
        }

        var now = DateTime.UtcNow;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        var recommendation = new Recommendation
        {
            Id = Guid.NewGuid(),
            OptimizationInsightId = model.OptimizationInsightId,
            LicenseId = model.LicenseId,
            Title = model.Title.Trim(),
            Description = model.Description.Trim(),
            Status = string.IsNullOrWhiteSpace(model.Status) ? "Open" : model.Status,
            EstimatedMonthlySavings = model.EstimatedMonthlySavings,
            EstimatedAnnualSavings = model.EstimatedAnnualSavings,
            Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = userId
        };

        _dbContext.Recommendations.Add(recommendation);
        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("Recommendation.Created", recommendation.Id.ToString(), $"Created recommendation: {recommendation.Title}");

        TempData["AlertMessage"] = "Recommendation created.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Details), new { id = recommendation.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var recommendation = await _dbContext.Recommendations
            .Include(r => r.License)
            .Include(r => r.OptimizationInsight)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recommendation is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var vm = new RecommendationDetailViewModel
        {
            Id = recommendation.Id,
            Title = recommendation.Title,
            Description = recommendation.Description,
            Status = recommendation.Status,
            EstimatedMonthlySavings = recommendation.EstimatedMonthlySavings,
            EstimatedAnnualSavings = recommendation.EstimatedAnnualSavings ?? (recommendation.EstimatedMonthlySavings.HasValue ? recommendation.EstimatedMonthlySavings.Value * 12 : null),
            Currency = recommendation.Currency,
            LicenseId = recommendation.LicenseId,
            LicenseName = recommendation.License?.Name,
            OptimizationInsightId = recommendation.OptimizationInsightId,
            InsightTitle = recommendation.OptimizationInsight?.Title,
            InsightEvidenceSummary = recommendation.OptimizationInsight is null
                ? null
                : OptimizationEvidenceFormatter.BuildSummary(recommendation.OptimizationInsight.Key, recommendation.OptimizationInsight.EvidenceJson),
            CreatedAtUtc = recommendation.CreatedAtUtc,
            UpdatedAtUtc = recommendation.UpdatedAtUtc,
            AlertMessage = TempData["AlertMessage"] as string,
            AlertStyle = TempData["AlertStyle"] as string ?? "info"
        };

        return View(vm);
    }

    [HttpGet("{id:guid}/edit")]
    [Authorize(Policy = PermissionPolicies.OptimizationManage)]
    public async Task<IActionResult> Edit(Guid id)
    {
        var recommendation = await _dbContext.Recommendations
            .Include(r => r.OptimizationInsight)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (recommendation is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var vm = new RecommendationFormViewModel
        {
            Id = recommendation.Id,
            OptimizationInsightId = recommendation.OptimizationInsightId,
            LicenseId = recommendation.LicenseId,
            Title = recommendation.Title,
            Description = recommendation.Description,
            Status = recommendation.Status,
            EstimatedMonthlySavings = recommendation.EstimatedMonthlySavings,
            EstimatedAnnualSavings = recommendation.EstimatedAnnualSavings,
            Currency = recommendation.Currency,
            Licenses = await LoadLicenseOptions(),
            StatusOptions = StatusOptions,
            InsightTitle = recommendation.OptimizationInsight?.Title,
            InsightEvidenceSummary = recommendation.OptimizationInsight is null
                ? null
                : OptimizationEvidenceFormatter.BuildSummary(recommendation.OptimizationInsight.Key, recommendation.OptimizationInsight.EvidenceJson)
        };

        return View(vm);
    }

    [HttpPost("{id:guid}/edit")]
    [Authorize(Policy = PermissionPolicies.OptimizationManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RecommendationFormViewModel model)
    {
        var recommendation = await _dbContext.Recommendations.FindAsync(id);
        if (recommendation is null)
        {
            return RedirectToAction(nameof(Index));
        }

        NormalizeSavings(model);

        if (!ValidateModel(model))
        {
            model.Licenses = await LoadLicenseOptions();
            model.StatusOptions = StatusOptions;
            return View(model);
        }

        var previousStatus = recommendation.Status;

        recommendation.Title = model.Title.Trim();
        recommendation.Description = model.Description.Trim();
        recommendation.Status = string.IsNullOrWhiteSpace(model.Status) ? "Open" : model.Status;
        recommendation.LicenseId = model.LicenseId;
        recommendation.OptimizationInsightId = model.OptimizationInsightId;
        recommendation.EstimatedMonthlySavings = model.EstimatedMonthlySavings;
        recommendation.EstimatedAnnualSavings = model.EstimatedAnnualSavings;
        recommendation.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "USD" : model.Currency.Trim().ToUpperInvariant();
        recommendation.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        await LogAuditAsync("Recommendation.Updated", recommendation.Id.ToString(), $"Updated recommendation: {recommendation.Title}");

        if (!string.Equals(previousStatus, recommendation.Status, StringComparison.OrdinalIgnoreCase))
        {
            await LogAuditAsync(
                "Recommendation.StatusChanged",
                recommendation.Id.ToString(),
                $"Recommendation status changed from {previousStatus} to {recommendation.Status}.");
        }

        TempData["AlertMessage"] = "Recommendation updated.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Details), new { id = recommendation.Id });
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = PermissionPolicies.OptimizationManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(Guid id, string status)
    {
        if (string.IsNullOrWhiteSpace(status) || !StatusOptions.Contains(status))
        {
            TempData["AlertMessage"] = "Select a valid status.";
            TempData["AlertStyle"] = "warning";
            return RedirectToAction(nameof(Details), new { id });
        }

        var recommendation = await _dbContext.Recommendations.FindAsync(id);
        if (recommendation is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(recommendation.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            TempData["AlertMessage"] = "Recommendation already has that status.";
            TempData["AlertStyle"] = "info";
            return RedirectToAction(nameof(Details), new { id });
        }

        var previousStatus = recommendation.Status;
        recommendation.Status = status;
        recommendation.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await LogAuditAsync(
            "Recommendation.StatusChanged",
            recommendation.Id.ToString(),
            $"Recommendation status changed from {previousStatus} to {recommendation.Status}.");

        TempData["AlertMessage"] = "Status updated.";
        TempData["AlertStyle"] = "success";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateFromInsightAsync(RecommendationFormViewModel vm, Guid insightId)
    {
        var insight = await _dbContext.OptimizationInsights
            .Include(i => i.License)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == insightId);

        if (insight is null)
        {
            vm.AlertMessage = "Insight not found. Create a recommendation manually.";
            vm.AlertStyle = "warning";
            return;
        }

        vm.OptimizationInsightId = insight.Id;
        vm.LicenseId = insight.LicenseId;
        vm.InsightTitle = insight.Title;
        vm.InsightEvidenceSummary = OptimizationEvidenceFormatter.BuildSummary(insight.Key, insight.EvidenceJson);

        var license = insight.License;
        if (license is null)
        {
            vm.Title = insight.Title;
            vm.Description = $"Opportunity identified: {vm.InsightEvidenceSummary}";
            return;
        }

        vm.Currency = string.IsNullOrWhiteSpace(license.Currency) ? "USD" : license.Currency;
        vm.Title = BuildRecommendationTitle(insight.Key, license.Name);
        vm.Description = BuildRecommendationDescription(insight.Key, license.Name, vm.InsightEvidenceSummary);

        var estimate = EstimateSavings(insight, license);
        if (estimate.Monthly.HasValue)
        {
            vm.EstimatedMonthlySavings = estimate.Monthly;
            vm.EstimatedAnnualSavings = estimate.Annual;
        }
    }

    private static (decimal? Monthly, decimal? Annual) EstimateSavings(OptimizationInsight insight, License license)
    {
        if (!license.CostPerSeatMonthly.HasValue || !license.SeatsPurchased.HasValue)
        {
            return (null, null);
        }

        var purchased = license.SeatsPurchased.Value;
        if (purchased <= 0)
        {
            return (null, null);
        }

        var evidence = OptimizationEvidenceFormatter.Parse(insight.EvidenceJson);
        var suggested = purchased;

        if (insight.Key.Equals("UnderutilizedSeats", StringComparison.OrdinalIgnoreCase))
        {
            var peak = evidence.PeakUsed ?? 0;
            var assigned = license.SeatsAssigned ?? 0;
            suggested = Math.Max(peak, assigned);
            if (suggested <= 0)
            {
                suggested = 1;
            }
        }
        else if (insight.Key.Equals("UnassignedSeats", StringComparison.OrdinalIgnoreCase) && license.SeatsAssigned.HasValue)
        {
            suggested = license.SeatsAssigned.Value;
        }

        var delta = purchased - suggested;
        if (delta <= 0)
        {
            return (null, null);
        }

        var monthly = delta * license.CostPerSeatMonthly.Value;
        return (monthly, monthly * 12);
    }

    private static string BuildRecommendationTitle(string key, string licenseName)
    {
        if (key.Equals("UnderutilizedSeats", StringComparison.OrdinalIgnoreCase))
        {
            return $"Reduce unused seats for {licenseName}";
        }

        if (key.Equals("UnassignedSeats", StringComparison.OrdinalIgnoreCase))
        {
            return $"Reclaim unassigned seats for {licenseName}";
        }

        return $"Optimization opportunity for {licenseName}";
    }

    private static string BuildRecommendationDescription(string key, string licenseName, string? evidenceSummary)
    {
        var summary = string.IsNullOrWhiteSpace(evidenceSummary) ? "Evidence summary unavailable." : evidenceSummary;

        if (key.Equals("UnderutilizedSeats", StringComparison.OrdinalIgnoreCase))
        {
            return $"Usage is consistently below purchased capacity for {licenseName}. {summary} Consider resizing the seat count to align with demand.";
        }

        if (key.Equals("UnassignedSeats", StringComparison.OrdinalIgnoreCase))
        {
            return $"There are seats purchased but not assigned for {licenseName}. {summary} Consider reclaiming or downgrading unused seats.";
        }

        return $"Review {licenseName} for cost optimization. {summary}";
    }

    private static void NormalizeSavings(RecommendationFormViewModel model)
    {
        if (model.EstimatedMonthlySavings.HasValue && !model.EstimatedAnnualSavings.HasValue)
        {
            model.EstimatedAnnualSavings = model.EstimatedMonthlySavings.Value * 12;
        }
    }

    private bool ValidateModel(RecommendationFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(model.Description))
        {
            ModelState.AddModelError(nameof(model.Description), "Description is required.");
        }

        if (model.EstimatedMonthlySavings.HasValue && model.EstimatedMonthlySavings.Value < 0)
        {
            ModelState.AddModelError(nameof(model.EstimatedMonthlySavings), "Monthly savings must be zero or greater.");
        }

        if (model.EstimatedAnnualSavings.HasValue && model.EstimatedAnnualSavings.Value < 0)
        {
            ModelState.AddModelError(nameof(model.EstimatedAnnualSavings), "Annual savings must be zero or greater.");
        }

        return ModelState.IsValid;
    }

    private async Task<IReadOnlyCollection<CategoryOption>> LoadCategoryOptions()
    {
        var categories = await _dbContext.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return categories.Select(c => new CategoryOption { Id = c.Id, Name = c.Name }).ToList();
    }

    private async Task<IReadOnlyCollection<LicenseOption>> LoadLicenseOptions()
    {
        var licenses = await _dbContext.Licenses.AsNoTracking()
            .OrderBy(l => l.Name)
            .ToListAsync();

        return licenses.Select(l => new LicenseOption { Id = l.Id, Name = l.Name }).ToList();
    }

    private async Task LogAuditAsync(string action, string entityId, string summary)
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
            EntityType = "Recommendation",
            EntityId = entityId,
            Summary = TrimToLength(summary, 500),
            IpAddress = ip
        });
    }

    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
