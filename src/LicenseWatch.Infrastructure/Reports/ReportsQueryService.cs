using LicenseWatch.Core.Services;
using LicenseWatch.Infrastructure.Bootstrap;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LicenseWatch.Infrastructure.Reports;

public class ReportsQueryService : IReportsQueryService
{
    private readonly AppDbContext _dbContext;
    private readonly IBootstrapSettingsStore _settingsStore;

    public ReportsQueryService(AppDbContext dbContext, IBootstrapSettingsStore settingsStore)
    {
        _dbContext = dbContext;
        _settingsStore = settingsStore;
    }

    public async Task<PagedResult<LicenseReportRow>> GetLicenseInventoryAsync(
        LicenseReportFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var thresholds = await GetSystemThresholdsAsync(cancellationToken);
        var query = BuildLicenseInventoryQuery(filter);
        var items = await query
            .OrderBy(l => l.Name)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.Vendor,
                CategoryName = l.Category!.Name,
                l.SeatsPurchased,
                l.SeatsAssigned,
                l.ExpiresOnUtc,
                l.UseCustomThresholds,
                l.CriticalThresholdDays,
                l.WarningThresholdDays
            })
            .ToListAsync(cancellationToken);

        var rows = items.Select(item =>
        {
            var resolved = ResolveThresholds(item.UseCustomThresholds, item.CriticalThresholdDays, item.WarningThresholdDays, thresholds);
            var status = LicenseStatusCalculator.ComputeStatus(item.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays);
            return new LicenseReportRow(
                item.Id,
                item.Name,
                item.Vendor,
                item.CategoryName,
                item.SeatsPurchased,
                item.SeatsAssigned,
                item.ExpiresOnUtc,
                status);
        });

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            rows = rows.Where(r => r.Status == filter.Status);
        }

        var totalCount = rows.Count();
        var paged = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<LicenseReportRow>(paged, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<LicenseReportRow>> GetLicenseInventoryExportAsync(
        LicenseReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var thresholds = await GetSystemThresholdsAsync(cancellationToken);
        var query = BuildLicenseInventoryQuery(filter);
        var items = await query
            .OrderBy(l => l.Name)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.Vendor,
                CategoryName = l.Category!.Name,
                l.SeatsPurchased,
                l.SeatsAssigned,
                l.ExpiresOnUtc,
                l.UseCustomThresholds,
                l.CriticalThresholdDays,
                l.WarningThresholdDays
            })
            .ToListAsync(cancellationToken);

        var rows = items.Select(item =>
        {
            var resolved = ResolveThresholds(item.UseCustomThresholds, item.CriticalThresholdDays, item.WarningThresholdDays, thresholds);
            var status = LicenseStatusCalculator.ComputeStatus(item.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays);
            return new LicenseReportRow(
                item.Id,
                item.Name,
                item.Vendor,
                item.CategoryName,
                item.SeatsPurchased,
                item.SeatsAssigned,
                item.ExpiresOnUtc,
                status);
        });

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            rows = rows.Where(r => r.Status == filter.Status);
        }

        return rows.ToList();
    }

    public async Task<PagedResult<ExpirationReportRow>> GetExpirationReportAsync(
        ExpirationReportFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var thresholds = await GetSystemThresholdsAsync(cancellationToken);
        var query = BuildExpirationQuery(filter);
        var totalCount = await query.CountAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;
        var items = await query
            .OrderBy(l => l.ExpiresOnUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.Vendor,
                ExpiresOnUtc = l.ExpiresOnUtc!.Value,
                l.UseCustomThresholds,
                l.CriticalThresholdDays,
                l.WarningThresholdDays
            })
            .ToListAsync(cancellationToken);

        var rows = items.Select(item =>
        {
            var daysRemaining = (item.ExpiresOnUtc.Date - today).Days;
            var resolved = ResolveThresholds(item.UseCustomThresholds, item.CriticalThresholdDays, item.WarningThresholdDays, thresholds);
            return new ExpirationReportRow(
                item.Id,
                item.Name,
                item.Vendor,
                item.ExpiresOnUtc,
                daysRemaining,
                LicenseStatusCalculator.ComputeStatus(item.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays));
        }).ToList();

        return new PagedResult<ExpirationReportRow>(rows, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<ExpirationReportRow>> GetExpirationReportExportAsync(
        ExpirationReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var thresholds = await GetSystemThresholdsAsync(cancellationToken);
        var query = BuildExpirationQuery(filter);
        var today = DateTime.UtcNow.Date;
        var items = await query
            .OrderBy(l => l.ExpiresOnUtc)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.Vendor,
                ExpiresOnUtc = l.ExpiresOnUtc!.Value,
                l.UseCustomThresholds,
                l.CriticalThresholdDays,
                l.WarningThresholdDays
            })
            .ToListAsync(cancellationToken);

        return items.Select(item =>
        {
            var daysRemaining = (item.ExpiresOnUtc.Date - today).Days;
            var resolved = ResolveThresholds(item.UseCustomThresholds, item.CriticalThresholdDays, item.WarningThresholdDays, thresholds);
            return new ExpirationReportRow(
                item.Id,
                item.Name,
                item.Vendor,
                item.ExpiresOnUtc,
                daysRemaining,
                LicenseStatusCalculator.ComputeStatus(item.ExpiresOnUtc, resolved.CriticalDays, resolved.WarningDays));
        }).ToList();
    }

    public async Task<PagedResult<ComplianceReportRow>> GetComplianceReportAsync(
        ComplianceReportFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildComplianceQuery(filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(v => v.DetectedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new ComplianceReportRow(
                v.Id,
                v.Severity,
                v.Title,
                v.Status,
                v.DetectedAtUtc,
                v.LicenseId,
                v.License != null ? v.License.Name : null,
                v.RuleKey))
            .ToListAsync(cancellationToken);

        return new PagedResult<ComplianceReportRow>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<ComplianceReportRow>> GetComplianceReportExportAsync(
        ComplianceReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = BuildComplianceQuery(filter);
        return await query
            .OrderByDescending(v => v.DetectedAtUtc)
            .Select(v => new ComplianceReportRow(
                v.Id,
                v.Severity,
                v.Title,
                v.Status,
                v.DetectedAtUtc,
                v.LicenseId,
                v.License != null ? v.License.Name : null,
                v.RuleKey))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<UsageReportRow>> GetUsageReportAsync(
        UsageReportFilter filter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildUsageQuery(filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<UsageReportRow>(items, totalCount, page, pageSize);
    }

    public async Task<IReadOnlyList<UsageReportRow>> GetUsageReportExportAsync(
        UsageReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = BuildUsageQuery(filter);
        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<LicenseWatch.Core.Entities.License> BuildLicenseInventoryQuery(LicenseReportFilter filter)
    {
        IQueryable<LicenseWatch.Core.Entities.License> query = _dbContext.Licenses.AsNoTracking().Include(l => l.Category);

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(l => l.CategoryId == filter.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Vendor))
        {
            query = query.Where(l => l.Vendor != null && l.Vendor.Contains(filter.Vendor));
        }

        if (filter.ExpiresFrom.HasValue)
        {
            var from = filter.ExpiresFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value >= from);
        }

        if (filter.ExpiresTo.HasValue)
        {
            var to = filter.ExpiresTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value <= to);
        }

        return query;
    }

    private IQueryable<LicenseWatch.Core.Entities.License> BuildExpirationQuery(ExpirationReportFilter filter)
    {
        var query = _dbContext.Licenses.AsNoTracking();
        var today = DateTime.UtcNow.Date;

        query = query.Where(l => l.ExpiresOnUtc.HasValue);

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(l => l.CategoryId == filter.CategoryId.Value);
        }

        if (filter.ExpiringDays.HasValue)
        {
            var end = today.AddDays(filter.ExpiringDays.Value);
            query = query.Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value.Date >= today && l.ExpiresOnUtc.Value.Date <= end);
        }

        if (filter.ExpiresFrom.HasValue)
        {
            var from = filter.ExpiresFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value >= from);
        }

        if (filter.ExpiresTo.HasValue)
        {
            var to = filter.ExpiresTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(l => l.ExpiresOnUtc.HasValue && l.ExpiresOnUtc.Value <= to);
        }

        return query;
    }

    private IQueryable<LicenseWatch.Core.Entities.ComplianceViolation> BuildComplianceQuery(ComplianceReportFilter filter)
    {
        IQueryable<LicenseWatch.Core.Entities.ComplianceViolation> query = _dbContext.ComplianceViolations.AsNoTracking().Include(v => v.License);

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(v => v.Status == filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Severity))
        {
            query = query.Where(v => v.Severity == filter.Severity);
        }

        if (!string.IsNullOrWhiteSpace(filter.Rule))
        {
            query = query.Where(v => v.RuleKey == filter.Rule);
        }

        return query;
    }

    private IQueryable<UsageReportRow> BuildUsageQuery(UsageReportFilter filter)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var windowEnd = filter.To ?? today;
        var windowStart = filter.From ?? windowEnd.AddDays(-29);
        if (windowStart > windowEnd)
        {
            (windowStart, windowEnd) = (windowEnd, windowStart);
        }

        var fromUtc = windowStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = windowEnd.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var summaries = _dbContext.UsageDailySummaries.AsNoTracking()
            .Where(u => u.UsageDateUtc >= fromUtc && u.UsageDateUtc <= toUtc);

        var grouped = summaries
            .GroupBy(u => u.LicenseId)
            .Select(g => new
            {
                LicenseId = g.Key,
                Peak = g.Max(x => x.MaxSeatsUsed),
                Avg = g.Average(x => x.MaxSeatsUsed),
                Count = g.Count()
            });

        IQueryable<LicenseWatch.Core.Entities.License> licenses = _dbContext.Licenses.AsNoTracking().Include(l => l.Category);

        if (filter.LicenseId.HasValue)
        {
            licenses = licenses.Where(l => l.Id == filter.LicenseId.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            licenses = licenses.Where(l => l.CategoryId == filter.CategoryId.Value);
        }

        var query =
            from agg in grouped
            join license in licenses on agg.LicenseId equals license.Id
            orderby license.Name
            select new UsageReportRow(
                license.Id,
                license.Name,
                license.Category != null ? license.Category.Name : "Unassigned",
                agg.Peak,
                Math.Round(agg.Avg, 1),
                agg.Count,
                windowStart,
                windowEnd);

        return query;
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
