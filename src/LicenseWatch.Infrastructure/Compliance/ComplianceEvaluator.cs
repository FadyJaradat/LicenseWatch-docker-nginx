using System.Text.Json;
using LicenseWatch.Core.Entities;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LicenseWatch.Infrastructure.Compliance;

public class ComplianceEvaluator : IComplianceEvaluator
{
    private const string RuleOveruse = "Overuse";
    private const string RuleExpired = "Expired";
    private const string RuleMissingSeats = "MissingSeats";

    private readonly AppDbContext _dbContext;
    private readonly ILogger<ComplianceEvaluator> _logger;

    public ComplianceEvaluator(AppDbContext dbContext, ILogger<ComplianceEvaluator> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ComplianceEvaluationResult> EvaluateAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var windowEnd = to ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var windowStart = from ?? windowEnd.AddDays(-29);
        if (windowStart > windowEnd)
        {
            (windowStart, windowEnd) = (windowEnd, windowStart);
        }

        var windowStartUtc = windowStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var windowEndUtc = windowEnd.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        var licenses = await _dbContext.Licenses.AsNoTracking().ToListAsync(cancellationToken);
        var usagePeaks = await LoadUsagePeaksAsync(windowStartUtc, windowEndUtc, cancellationToken);

        var evaluatedRules = new[] { RuleOveruse, RuleExpired, RuleMissingSeats };
        var existingViolations = await _dbContext.ComplianceViolations
            .Where(v => v.LicenseId != null && evaluatedRules.Contains(v.RuleKey))
            .ToListAsync(cancellationToken);

        var violationsByKey = existingViolations.ToDictionary(
            v => (v.LicenseId!.Value, v.RuleKey),
            v => v);

        var triggeredKeys = new HashSet<(Guid LicenseId, string RuleKey)>();
        var opened = 0;
        var resolved = 0;
        var updated = 0;

        foreach (var license in licenses)
        {
            var usage = usagePeaks.TryGetValue(license.Id, out var peakInfo)
                ? peakInfo
                : UsagePeak.FromSeatsAssigned(license.SeatsAssigned);

            if (license.SeatsPurchased.HasValue && license.SeatsPurchased.Value > 0 &&
                usage.PeakUsed.HasValue && usage.PeakUsed.Value > license.SeatsPurchased.Value)
            {
                var evidence = new Dictionary<string, object?>
                {
                    ["seatsPurchased"] = license.SeatsPurchased.Value,
                    ["peakUsed"] = usage.PeakUsed.Value,
                    ["dateOfPeak"] = usage.DateOfPeak?.ToString("yyyy-MM-dd"),
                    ["windowDays"] = (windowEnd.DayNumber - windowStart.DayNumber) + 1,
                    ["source"] = usage.Source
                };

                var title = "Seat overuse detected";
                var details = $"Peak usage of {usage.PeakUsed.Value} seats exceeds the {license.SeatsPurchased.Value} purchased.";
                UpsertViolation(license.Id, RuleOveruse, "Critical", title, details, evidence, now, violationsByKey, ref opened, ref updated, triggeredKeys);
            }

            if (license.ExpiresOnUtc.HasValue && license.ExpiresOnUtc.Value.Date <= DateTime.UtcNow.Date)
            {
                var daysPastDue = (DateTime.UtcNow.Date - license.ExpiresOnUtc.Value.Date).Days;
                var evidence = new Dictionary<string, object?>
                {
                    ["expiresOn"] = license.ExpiresOnUtc.Value.ToString("yyyy-MM-dd"),
                    ["daysPastDue"] = daysPastDue
                };

                var title = "License expired";
                var details = $"Expired on {license.ExpiresOnUtc.Value:yyyy-MM-dd} ({daysPastDue} days past due).";
                UpsertViolation(license.Id, RuleExpired, "Critical", title, details, evidence, now, violationsByKey, ref opened, ref updated, triggeredKeys);
            }

            if (!license.SeatsPurchased.HasValue && usage.PeakUsed.HasValue && usage.PeakUsed.Value > 0)
            {
                var evidence = new Dictionary<string, object?>
                {
                    ["peakUsed"] = usage.PeakUsed.Value,
                    ["dateOfPeak"] = usage.DateOfPeak?.ToString("yyyy-MM-dd"),
                    ["windowDays"] = (windowEnd.DayNumber - windowStart.DayNumber) + 1,
                    ["source"] = usage.Source
                };

                var title = "Seats purchased missing";
                var details = "Usage was detected but seats purchased is not configured.";
                UpsertViolation(license.Id, RuleMissingSeats, "Warning", title, details, evidence, now, violationsByKey, ref opened, ref updated, triggeredKeys);
            }
        }

        foreach (var violation in existingViolations)
        {
            var key = (violation.LicenseId!.Value, violation.RuleKey);
            if (!triggeredKeys.Contains(key) && violation.Status != "Resolved")
            {
                violation.Status = "Resolved";
                violation.ResolvedAtUtc = now;
                violation.LastEvaluatedAtUtc = now;
                resolved++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var totalOpen = await _dbContext.ComplianceViolations.CountAsync(v => v.Status == "Open", cancellationToken);
        var totalAcknowledged = await _dbContext.ComplianceViolations.CountAsync(v => v.Status == "Acknowledged", cancellationToken);
        var totalResolved = await _dbContext.ComplianceViolations.CountAsync(v => v.Status == "Resolved", cancellationToken);

        _logger.LogInformation("Compliance evaluation complete. Opened {Opened}, Resolved {Resolved}, Updated {Updated}", opened, resolved, updated);

        return new ComplianceEvaluationResult(windowStart, windowEnd, opened, resolved, updated, totalOpen, totalAcknowledged, totalResolved);
    }

    private async Task<Dictionary<Guid, UsagePeak>> LoadUsagePeaksAsync(DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken cancellationToken)
    {
        var summaries = await _dbContext.UsageDailySummaries.AsNoTracking()
            .Where(u => u.UsageDateUtc >= windowStartUtc && u.UsageDateUtc <= windowEndUtc)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, UsagePeak>();
        foreach (var group in summaries.GroupBy(s => s.LicenseId))
        {
            var peak = group
                .OrderByDescending(s => s.MaxSeatsUsed)
                .ThenByDescending(s => s.UsageDateUtc)
                .First();

            result[group.Key] = new UsagePeak(peak.MaxSeatsUsed, peak.UsageDateUtc, "UsageDailySummary");
        }

        return result;
    }

    private void UpsertViolation(
        Guid licenseId,
        string ruleKey,
        string severity,
        string title,
        string details,
        Dictionary<string, object?> evidence,
        DateTime now,
        Dictionary<(Guid LicenseId, string RuleKey), ComplianceViolation> existing,
        ref int opened,
        ref int updated,
        HashSet<(Guid LicenseId, string RuleKey)> triggered)
    {
        triggered.Add((licenseId, ruleKey));
        if (existing.TryGetValue((licenseId, ruleKey), out var violation))
        {
            violation.Severity = severity;
            violation.Title = TrimToLength(title, 200);
            violation.Details = TrimToLength(details, 1000);
            violation.EvidenceJson = TrimToLength(JsonSerializer.Serialize(evidence), 2000);
            violation.LastEvaluatedAtUtc = now;

            if (violation.Status == "Resolved")
            {
                violation.Status = "Open";
                violation.DetectedAtUtc = now;
                violation.AcknowledgedAtUtc = null;
                violation.AcknowledgedByUserId = null;
                violation.ResolvedAtUtc = null;
                opened++;
            }
            else
            {
                updated++;
            }

            return;
        }

        var created = new ComplianceViolation
        {
            Id = Guid.NewGuid(),
            LicenseId = licenseId,
            RuleKey = ruleKey,
            Severity = severity,
            Status = "Open",
            Title = TrimToLength(title, 200),
            Details = TrimToLength(details, 1000),
            EvidenceJson = TrimToLength(JsonSerializer.Serialize(evidence), 2000),
            DetectedAtUtc = now,
            LastEvaluatedAtUtc = now
        };

        _dbContext.ComplianceViolations.Add(created);
        existing[(licenseId, ruleKey)] = created;
        opened++;
    }

    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record UsagePeak(int? PeakUsed, DateTime? DateOfPeak, string Source)
    {
        public static UsagePeak FromSeatsAssigned(int? seatsAssigned)
        {
            return seatsAssigned.HasValue
                ? new UsagePeak(seatsAssigned.Value, DateTime.UtcNow.Date, "SeatsAssigned")
                : new UsagePeak(null, null, "None");
        }
    }
}
