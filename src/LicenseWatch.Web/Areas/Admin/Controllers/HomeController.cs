using LicenseWatch.Infrastructure.Dashboard;
using LicenseWatch.Web.Models.Admin;
using LicenseWatch.Web.Models.Ui;
using Microsoft.AspNetCore.Authorization;
using LicenseWatch.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace LicenseWatch.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = PermissionPolicies.DashboardView)]
public class HomeController : Controller
{
    private readonly IDashboardQueryService _dashboardQueryService;

    public HomeController(IDashboardQueryService dashboardQueryService)
    {
        _dashboardQueryService = dashboardQueryService;
    }

    public async Task<IActionResult> Index(int? rangeDays = null)
    {
        var normalizedRange = NormalizeRangeDays(rangeDays);
        var snapshot = await _dashboardQueryService.GetSnapshotAsync(normalizedRange);
        var nextRenewal = snapshot.ExpiringSoon.OrderBy(item => item.ExpiresOnUtc).FirstOrDefault();

        var vm = new DashboardViewModel
        {
            TotalLicenses = snapshot.Kpis.TotalLicenses,
            Expired = snapshot.Kpis.Expired,
            ExpiringSoon = snapshot.Kpis.ExpiringSoon,
            OverAllocated = snapshot.Kpis.OverAllocated,
            OveruseRisk = snapshot.Kpis.OveruseRisk,
            NextRenewalName = nextRenewal?.Name,
            NextRenewalDays = nextRenewal?.DaysLeft,
            NextRenewalDateUtc = nextRenewal?.ExpiresOnUtc,
            NextRenewalUrl = nextRenewal is null ? null : $"/admin/licenses/{nextRenewal.LicenseId}",
            RangeDays = normalizedRange,
            LastRefreshedUtc = DateTime.UtcNow,
            Kpis = BuildKpis(snapshot.Kpis),
            CriticalAttention = snapshot.Attention
                .Where(item => string.Equals(item.Severity, "Critical", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(item => new AttentionItemViewModel
                {
                    Severity = item.Severity,
                    Title = item.Title,
                    Summary = item.Summary,
                    TargetUrl = item.TargetUrl
                }).ToList(),
            WarningAttention = snapshot.Attention
                .Where(item => string.Equals(item.Severity, "Warning", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .Select(item => new AttentionItemViewModel
                {
                    Severity = item.Severity,
                    Title = item.Title,
                    Summary = item.Summary,
                    TargetUrl = item.TargetUrl
                }).ToList(),
            RecentActivity = snapshot.RecentActivity.Select(item => new ActivityFeedItemViewModel
            {
                OccurredAtUtc = item.OccurredAtUtc,
                Actor = item.ActorEmail,
                Action = item.Action,
                Summary = item.Summary,
                Icon = ActivityIcon(item.Action),
                TargetUrl = ResolveActivityTarget(item.EntityType, item.EntityId)
            }).ToList(),
            StatusCounts = snapshot.StatusCounts.Select(s => new ChartPointViewModel { Label = s.Status, Value = s.Count }).ToList(),
            ExpirationTrend = snapshot.ExpirationTrend.Select(t => new TrendBucketViewModel
            {
                Label = t.Label,
                Count = t.Count,
                ExpiringDays = t.ExpiringDays
            }).ToList(),
            VendorCounts = snapshot.TopVendors.Select(v => new VendorCountViewModel
            {
                Vendor = v.Vendor,
                Count = v.Count
            }).ToList()
        };

        return View(vm);
    }

    private static int NormalizeRangeDays(int? rangeDays)
        => rangeDays switch
        {
            7 => 7,
            30 => 30,
            90 => 90,
            _ => 90
        };

    private static IReadOnlyCollection<KpiCardViewModel> BuildKpis(DashboardKpis kpis)
    {
        var criticalRisk = kpis.Expired + kpis.OveruseRisk;
        var allocationValue = kpis.OverAllocated;
        var allocationLabel = allocationValue.HasValue ? allocationValue.Value.ToString("N0") : "N/A";
        var allocationIsZero = allocationValue.HasValue && allocationValue.Value == 0;
        var allocationSeverity = allocationValue.HasValue && allocationValue.Value > 0 ? "warning" : "neutral";
        var allocationSubtext = allocationValue.HasValue
            ? allocationValue.Value > 0 ? "Assignments exceed purchases" : "No allocation gaps"
            : "Seat tracking not configured";

        return new List<KpiCardViewModel>
        {
            new()
            {
                Label = "Licenses at risk",
                Value = criticalRisk.ToString("N0"),
                NumericValue = criticalRisk,
                Subtext = "Expired or overused",
                Icon = "bi-exclamation-octagon",
                Href = "/admin/licenses?criticalRisk=true",
                Severity = "critical",
                IsZero = criticalRisk == 0
            },
            new()
            {
                Label = "Renewals needing action",
                Value = kpis.ExpiringSoon.ToString("N0"),
                NumericValue = kpis.ExpiringSoon,
                Subtext = "Next 30 days",
                Icon = "bi-calendar-event",
                Href = "/admin/licenses?expiringDays=30",
                Severity = "warning",
                IsZero = kpis.ExpiringSoon == 0
            },
            new()
            {
                Label = "Allocation exceptions",
                Value = allocationLabel,
                NumericValue = allocationValue,
                Subtext = allocationSubtext,
                Icon = "bi-diagram-3",
                Href = "/admin/licenses?overAllocated=true",
                Severity = allocationSeverity,
                IsZero = allocationIsZero
            },
            new()
            {
                Label = "Licenses tracked",
                Value = kpis.TotalLicenses.ToString("N0"),
                NumericValue = kpis.TotalLicenses,
                Subtext = "Active portfolio",
                Icon = "bi-collection",
                Href = "/admin/licenses",
                Severity = "neutral",
                IsZero = kpis.TotalLicenses == 0
            }
        };
    }

    private static string ActivityIcon(string action)
    {
        if (action.Contains("License", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-file-earmark-text";
        }

        if (action.Contains("Compliance", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-shield-exclamation";
        }

        if (action.Contains("Import", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-cloud-arrow-up";
        }

        if (action.Contains("Email", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-envelope";
        }

        if (action.Contains("Optimization", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-lightbulb";
        }

        if (action.Contains("Security", StringComparison.OrdinalIgnoreCase))
        {
            return "bi-shield-check";
        }

        return "bi-activity";
    }

    private static string? ResolveActivityTarget(string entityType, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            return null;
        }

        return entityType switch
        {
            "License" => $"/admin/licenses/{entityId}",
            "Category" => "/admin/categories",
            "Compliance" or "ComplianceViolation" => "/admin/compliance",
            "ImportSession" => "/admin/import",
            "EmailTemplate" or "NotificationLog" => "/admin/email/log",
            "Recommendation" => $"/admin/optimization/recommendations/{entityId}",
            "OptimizationInsight" => "/admin/optimization",
            "JobExecutionLog" => "/admin/jobs",
            "Security" => "/admin/security",
            "AppDb" => "/admin/migrations",
            _ => "/admin/audit"
        };
    }
}
