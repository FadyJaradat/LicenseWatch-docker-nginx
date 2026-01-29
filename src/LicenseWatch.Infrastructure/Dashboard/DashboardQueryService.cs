using LicenseWatch.Core.Services;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Infrastructure.Dashboard;

public class DashboardQueryService : IDashboardQueryService
{
    private readonly AppDbContext _dbContext;
    private readonly IBootstrapSettingsStore _settingsStore;

    public DashboardQueryService(AppDbContext dbContext, IBootstrapSettingsStore settingsStore)
    {
        _dbContext = dbContext;
        _settingsStore = settingsStore;
    }

    public async Task<DashboardSnapshot> GetSnapshotAsync(int? rangeDays = null, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var horizonDays = NormalizeRangeDays(rangeDays);
        var thresholds = await GetSystemThresholdsAsync(cancellationToken);

        var licenseThresholds = await _dbContext.Licenses.AsNoTracking()
            .Select(l => new
            {
                l.ExpiresOnUtc,
                l.UseCustomThresholds,
                l.CriticalThresholdDays,
                l.WarningThresholdDays
            })
            .ToListAsync(cancellationToken);

        var totalLicenses = licenseThresholds.Count;
        var expiredCount = 0;
        var warningCount = 0;
        var criticalCount = 0;
        var goodCount = 0;
        var unknownCount = 0;

        foreach (var license in licenseThresholds)
        {
            var resolved = ResolveThresholds(license.UseCustomThresholds, license.CriticalThresholdDays, license.WarningThresholdDays, thresholds);
            var status = LicenseStatusCalculator.ComputeStatus(license.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays, today);
            switch (status)
            {
                case "Expired":
                    expiredCount++;
                    break;
                case "Critical":
                    criticalCount++;
                    break;
                case "Warning":
                    warningCount++;
                    break;
                case "Good":
                    goodCount++;
                    break;
                default:
                    unknownCount++;
                    break;
            }
        }

        var expiringCount = warningCount + criticalCount;

        var allocationTrackedCount = await _dbContext.Licenses.AsNoTracking()
            .CountAsync(l => l.SeatsAssigned.HasValue && l.SeatsPurchased.HasValue, cancellationToken);
        var overAllocatedCount = await _dbContext.Licenses.AsNoTracking()
            .CountAsync(l => l.SeatsAssigned.HasValue && l.SeatsPurchased.HasValue && l.SeatsAssigned.Value > l.SeatsPurchased.Value, cancellationToken);

        int? overAllocated = allocationTrackedCount == 0 ? null : overAllocatedCount;

        var overuseRisk = await GetOveruseRiskCount(today, cancellationToken);

        var expiringSoon = await _dbContext.Licenses.AsNoTracking()
            .Include(l => l.Category)
            .Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date > today && l.ExpiresOnUtc.Value.Date <= today.AddDays(horizonDays))
            .OrderBy(l => l.ExpiresOnUtc)
            .Take(15)
            .Select(l => new
            {
                l.Id,
                l.Name,
                CategoryName = l.Category != null ? l.Category.Name : "Unassigned",
                ExpiresOnUtc = l.ExpiresOnUtc!.Value,
                l.UseCustomThresholds,
                l.CriticalThresholdDays,
                l.WarningThresholdDays
            })
            .ToListAsync(cancellationToken);

        var expiringSoonItems = expiringSoon.Select(item =>
        {
            var daysLeft = (item.ExpiresOnUtc.Date - today).Days;
            var resolved = ResolveThresholds(item.UseCustomThresholds, item.CriticalThresholdDays, item.WarningThresholdDays, thresholds);
            return new ExpiringSoonItem(
                item.Id,
                item.Name,
                item.CategoryName,
                item.ExpiresOnUtc,
                daysLeft,
                LicenseStatusCalculator.ComputeStatus(item.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays));
        }).ToList();

        var attentionItems = await BuildAttentionItems(today, horizonDays, thresholds, cancellationToken);
        var statusCounts = BuildStatusCounts(goodCount, warningCount, criticalCount, expiredCount, unknownCount);
        var expirationsByMonth = await GetExpirationsByMonth(today, cancellationToken);
        var expirationTrend = await GetExpirationTrend(today, horizonDays, cancellationToken);
        var topVendors = await GetTopVendors(cancellationToken);

        var recentActivity = await _dbContext.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(10)
            .Select(a => new RecentAuditItem(a.OccurredAtUtc, a.ActorEmail, a.Action, a.Summary, a.EntityType, a.EntityId))
            .ToListAsync(cancellationToken);

        return new DashboardSnapshot(
            new DashboardKpis(totalLicenses, expiredCount, expiringCount, overAllocated, overuseRisk),
            expiringSoonItems,
            attentionItems,
            statusCounts,
            expirationsByMonth,
            expirationTrend,
            topVendors,
            recentActivity);
    }

    private static IReadOnlyCollection<StatusCount> BuildStatusCounts(int good, int warning, int critical, int expired, int unknown)
    {
        return new List<StatusCount>
        {
            new("Good", good),
            new("Warning", warning),
            new("Critical", critical),
            new("Expired", expired),
            new("Unknown", unknown)
        };
    }

    private async Task<IReadOnlyCollection<MonthlyExpirationCount>> GetExpirationsByMonth(DateTime today, CancellationToken cancellationToken)
    {
        var start = new DateTime(today.Year, today.Month, 1);
        var end = start.AddMonths(12);

        var upcoming = await _dbContext.Licenses.AsNoTracking()
            .Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date >= start && l.ExpiresOnUtc.Value.Date < end)
            .Select(l => l.ExpiresOnUtc!.Value.Date)
            .ToListAsync(cancellationToken);

        var grouped = upcoming.GroupBy(d => new { d.Year, d.Month })
            .ToDictionary(g => $"{g.Key.Year:D4}-{g.Key.Month:D2}", g => g.Count());

        var results = new List<MonthlyExpirationCount>();
        for (var i = 0; i < 12; i++)
        {
            var month = start.AddMonths(i);
            var key = $"{month.Year:D4}-{month.Month:D2}";
            grouped.TryGetValue(key, out var count);
            results.Add(new MonthlyExpirationCount(month.ToString("MMM yy"), count));
        }

        return results;
    }

    private async Task<int> GetOveruseRiskCount(DateTime today, CancellationToken cancellationToken)
    {
        var windowStart = today.AddDays(-30);

        var usagePeaks = _dbContext.UsageDailySummaries.AsNoTracking()
            .Where(u => u.UsageDateUtc >= windowStart)
            .GroupBy(u => u.LicenseId)
            .Select(g => new { LicenseId = g.Key, Peak = g.Max(x => x.MaxSeatsUsed) });

        var licensed = _dbContext.Licenses.AsNoTracking()
            .Where(l => l.SeatsPurchased.HasValue && l.SeatsPurchased.Value > 0)
            .Select(l => new { l.Id, l.SeatsPurchased });

        return await usagePeaks
            .Join(licensed, peak => peak.LicenseId, lic => lic.Id, (peak, lic) => new { peak.Peak, lic.SeatsPurchased })
            .CountAsync(x => x.Peak > x.SeatsPurchased!.Value, cancellationToken);
    }

    private async Task<IReadOnlyCollection<AttentionItem>> BuildAttentionItems(
        DateTime today,
        int horizonDays,
        (int CriticalDays, int WarningDays) thresholds,
        CancellationToken cancellationToken)
    {
        var expiringEnd = today.AddDays(horizonDays);

        var compliance = await _dbContext.ComplianceViolations.AsNoTracking()
            .Include(v => v.License)
            .Where(v => v.Status == "Open" && (v.Severity == "Critical" || v.Severity == "Warning"))
            .OrderByDescending(v => v.DetectedAtUtc)
            .Take(20)
            .Select(v => new
            {
                v.Severity,
                v.Title,
                v.Details,
                v.LicenseId,
                LicenseName = v.License != null ? v.License.Name : null
            })
            .ToListAsync(cancellationToken);

        var expired = await _dbContext.Licenses.AsNoTracking()
            .Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date <= today)
            .OrderByDescending(l => l.ExpiresOnUtc)
            .Take(10)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.ExpiresOnUtc,
                l.UseCustomThresholds,
                l.CriticalThresholdDays,
                l.WarningThresholdDays
            })
            .ToListAsync(cancellationToken);

        var expiring = await _dbContext.Licenses.AsNoTracking()
            .Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date > today && l.ExpiresOnUtc.Value.Date <= expiringEnd)
            .OrderBy(l => l.ExpiresOnUtc)
            .Take(10)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.ExpiresOnUtc,
                l.UseCustomThresholds,
                l.CriticalThresholdDays,
                l.WarningThresholdDays
            })
            .ToListAsync(cancellationToken);

        var items = new List<AttentionItem>();

        foreach (var item in compliance)
        {
            var summary = string.IsNullOrWhiteSpace(item.Details) ? item.Title : item.Details;
            var url = item.LicenseId.HasValue
                ? $"/admin/licenses/{item.LicenseId}"
                : $"/admin/compliance?status=Open&severity={item.Severity}";

            items.Add(new AttentionItem(
                item.Severity,
                item.Title,
                summary,
                item.LicenseId,
                url));
        }

        foreach (var item in expired)
        {
            var dateLabel = item.ExpiresOnUtc!.Value.ToString("MMM dd, yyyy");
            items.Add(new AttentionItem(
                "Critical",
                item.Name,
                $"Expired on {dateLabel}.",
                item.Id,
                $"/admin/licenses/{item.Id}"));
        }

        foreach (var item in expiring)
        {
            var daysLeft = (item.ExpiresOnUtc!.Value.Date - today).Days;
            var dateLabel = item.ExpiresOnUtc!.Value.ToString("MMM dd, yyyy");
            var resolved = ResolveThresholds(item.UseCustomThresholds, item.CriticalThresholdDays, item.WarningThresholdDays, thresholds);
            var status = LicenseStatusCalculator.ComputeStatus(item.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays, today);
            var severity = status is "Critical" or "Warning" ? status : "Warning";
            items.Add(new AttentionItem(
                severity,
                item.Name,
                $"Expires in {daysLeft} days on {dateLabel}.",
                item.Id,
                $"/admin/licenses/{item.Id}"));
        }

        return items;
    }

    private async Task<IReadOnlyCollection<TrendBucket>> GetExpirationTrend(DateTime today, int horizonDays, CancellationToken cancellationToken)
    {
        var end = today.AddDays(horizonDays);
        var dates = await _dbContext.Licenses.AsNoTracking()
            .Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date > today && l.ExpiresOnUtc.Value.Date <= end)
            .Select(l => l.ExpiresOnUtc!.Value.Date)
            .ToListAsync(cancellationToken);

        var bucketEnds = BuildBucketEnds(horizonDays);
        var buckets = new List<TrendBucket>();
        foreach (var bucketEnd in bucketEnds)
        {
            var endDate = today.AddDays(bucketEnd);
            var count = dates.Count(d => d <= endDate);
            buckets.Add(new TrendBucket($"Next {bucketEnd} days", count, bucketEnd));
        }

        return buckets;
    }

    private async Task<IReadOnlyCollection<VendorCount>> GetTopVendors(CancellationToken cancellationToken)
    {
        var vendors = await _dbContext.Licenses.AsNoTracking()
            .GroupBy(l => l.Vendor == null || l.Vendor == string.Empty ? "Unspecified" : l.Vendor)
            .Select(g => new { Vendor = g.Key, Count = g.Count() })
            .OrderByDescending(v => v.Count)
            .ThenBy(v => v.Vendor)
            .Take(5)
            .ToListAsync(cancellationToken);

        return vendors
            .Select(v => new VendorCount(v.Vendor ?? "Unspecified", v.Count))
            .ToList();
    }

    private static int NormalizeRangeDays(int? rangeDays)
        => rangeDays switch
        {
            7 => 7,
            30 => 30,
            90 => 90,
            _ => 90
        };

    private static IReadOnlyList<int> BuildBucketEnds(int rangeDays)
    {
        return rangeDays switch
        {
            7 => new[] { 1, 3, 7 },
            30 => new[] { 10, 20, 30 },
            _ => new[] { 30, 60, 90 }
        };
    }

    private async Task<(int CriticalDays, int WarningDays)> GetSystemThresholdsAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.LoadAsync(cancellationToken);
        return LicenseStatusCalculator.NormalizeThresholds(
            settings.Compliance.CriticalDays,
            settings.Compliance.WarningDays);
    }

    private static (int CriticalDays, int WarningDays) ResolveThresholds(
        bool useCustom,
        int? criticalOverride,
        int? warningOverride,
        (int CriticalDays, int WarningDays) systemThresholds)
    {
        if (!useCustom)
        {
            return systemThresholds;
        }

        return LicenseStatusCalculator.NormalizeThresholds(
            criticalOverride ?? systemThresholds.CriticalDays,
            warningOverride ?? systemThresholds.WarningDays);
    }

}
