using System.Text.Json;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Infrastructure.Optimization;

public class OptimizationEngine : IOptimizationEngine
{
    private const string UnderutilizedKey = "UnderutilizedSeats";
    private const string UnassignedKey = "UnassignedSeats";

    private readonly AppDbContext _dbContext;
    private readonly ILogger<OptimizationEngine> _logger;

    public OptimizationEngine(AppDbContext dbContext, ILogger<OptimizationEngine> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<OptimizationResult> GenerateInsightsAsync(int windowDays = 30, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowEnd = DateTime.UtcNow.Date;
        var windowStart = windowEnd.AddDays(-(Math.Max(windowDays, 1) - 1));

        var usage = await _dbContext.UsageDailySummaries.AsNoTracking()
            .Where(u => u.UsageDateUtc >= windowStart && u.UsageDateUtc <= windowEnd)
            .GroupBy(u => u.LicenseId)
            .Select(g => new
            {
                LicenseId = g.Key,
                Peak = g.Max(x => x.MaxSeatsUsed)
            })
            .ToListAsync(cancellationToken);

        var peakLookup = usage.ToDictionary(x => x.LicenseId, x => x.Peak);

        var licenses = await _dbContext.Licenses.AsNoTracking()
            .Include(l => l.Category)
            .ToListAsync(cancellationToken);

        var keys = new[] { UnderutilizedKey, UnassignedKey };
        var existing = await _dbContext.OptimizationInsights
            .Where(i => keys.Contains(i.Key))
            .ToListAsync(cancellationToken);

        var insightLookup = existing
            .Where(i => i.LicenseId.HasValue)
            .ToDictionary(i => (i.Key, i.LicenseId!.Value), i => i);

        var triggered = new HashSet<(string Key, Guid LicenseId)>();
        var created = 0;
        var updated = 0;
        var deactivated = 0;

        foreach (var license in licenses)
        {
            if (license.SeatsPurchased.HasValue && license.SeatsPurchased.Value > 0)
            {
                var peakUsed = peakLookup.TryGetValue(license.Id, out var peak) ? peak : 0;
                var utilization = (double)peakUsed / license.SeatsPurchased.Value;
                var utilizationPercent = Math.Round(utilization * 100, 1);

                if (utilization <= 0.30)
                {
                    var severity = utilization <= 0.10 ? "Critical" : "Warning";
                    var evidence = new Dictionary<string, object?>
                    {
                        ["seatsPurchased"] = license.SeatsPurchased.Value,
                        ["peakUsed"] = peakUsed,
                        ["utilizationPercent"] = utilizationPercent,
                        ["windowDays"] = windowDays
                    };

                    var title = "Low seat utilization detected";
                    UpsertInsight(
                        license,
                        UnderutilizedKey,
                        title,
                        severity,
                        evidence,
                        now,
                        insightLookup,
                        triggered,
                        ref created,
                        ref updated);
                }
            }

            if (license.SeatsPurchased.HasValue && license.SeatsAssigned.HasValue)
            {
                var purchased = license.SeatsPurchased.Value;
                var assigned = license.SeatsAssigned.Value;
                var unassigned = purchased - assigned;
                if (unassigned >= 5 || (purchased > 0 && unassigned >= (int)Math.Ceiling(purchased * 0.20)))
                {
                    var evidence = new Dictionary<string, object?>
                    {
                        ["seatsPurchased"] = purchased,
                        ["seatsAssigned"] = assigned,
                        ["unassigned"] = unassigned
                    };

                    var title = "Unassigned seats available";
                    UpsertInsight(
                        license,
                        UnassignedKey,
                        title,
                        "Warning",
                        evidence,
                        now,
                        insightLookup,
                        triggered,
                        ref created,
                        ref updated);
                }
            }
        }

        foreach (var insight in existing)
        {
            if (!insight.LicenseId.HasValue)
            {
                continue;
            }

            var key = (insight.Key, insight.LicenseId.Value);
            if (insight.IsActive && !triggered.Contains(key))
            {
                insight.IsActive = false;
                deactivated++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Optimization analysis finished. Created {Created}, Updated {Updated}, Deactivated {Deactivated}", created, updated, deactivated);

        return new OptimizationResult(created, updated, deactivated);
    }

    private void UpsertInsight(
        License license,
        string key,
        string title,
        string severity,
        Dictionary<string, object?> evidence,
        DateTime now,
        Dictionary<(string Key, Guid LicenseId), OptimizationInsight> lookup,
        HashSet<(string Key, Guid LicenseId)> triggered,
        ref int created,
        ref int updated)
    {
        var lookupKey = (key, license.Id);
        triggered.Add(lookupKey);

        var evidenceJson = JsonSerializer.Serialize(evidence);
        if (lookup.TryGetValue(lookupKey, out var existing))
        {
            existing.Title = title;
            existing.Severity = severity;
            existing.EvidenceJson = evidenceJson;
            existing.IsActive = true;
            existing.CategoryId = license.CategoryId;
            updated++;
            return;
        }

        var insight = new OptimizationInsight
        {
            Id = Guid.NewGuid(),
            Key = key,
            Title = title,
            Severity = severity,
            LicenseId = license.Id,
            CategoryId = license.CategoryId,
            DetectedAtUtc = now,
            EvidenceJson = evidenceJson,
            IsActive = true
        };

        _dbContext.OptimizationInsights.Add(insight);
        lookup[lookupKey] = insight;
        created++;
    }
}
