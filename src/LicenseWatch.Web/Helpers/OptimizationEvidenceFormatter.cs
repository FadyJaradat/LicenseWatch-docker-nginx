using System.Text.Json;

namespace LicenseWatch.Web.Helpers;

public static class OptimizationEvidenceFormatter
{
    public static OptimizationEvidenceSnapshot Parse(string? evidenceJson)
    {
        if (string.IsNullOrWhiteSpace(evidenceJson))
        {
            return new OptimizationEvidenceSnapshot();
        }

        try
        {
            using var doc = JsonDocument.Parse(evidenceJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new OptimizationEvidenceSnapshot();
            }

            int? seatsPurchased = null;
            int? seatsAssigned = null;
            int? unassigned = null;
            int? peakUsed = null;
            double? utilizationPercent = null;
            int? windowDays = null;

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "seatsPurchased" when property.Value.ValueKind == JsonValueKind.Number:
                        seatsPurchased = property.Value.GetInt32();
                        break;
                    case "seatsAssigned" when property.Value.ValueKind == JsonValueKind.Number:
                        seatsAssigned = property.Value.GetInt32();
                        break;
                    case "unassigned" when property.Value.ValueKind == JsonValueKind.Number:
                        unassigned = property.Value.GetInt32();
                        break;
                    case "peakUsed" when property.Value.ValueKind == JsonValueKind.Number:
                        peakUsed = property.Value.GetInt32();
                        break;
                    case "utilizationPercent" when property.Value.ValueKind == JsonValueKind.Number:
                        utilizationPercent = property.Value.GetDouble();
                        break;
                    case "windowDays" when property.Value.ValueKind == JsonValueKind.Number:
                        windowDays = property.Value.GetInt32();
                        break;
                }
            }

            return new OptimizationEvidenceSnapshot
            {
                SeatsPurchased = seatsPurchased,
                SeatsAssigned = seatsAssigned,
                Unassigned = unassigned,
                PeakUsed = peakUsed,
                UtilizationPercent = utilizationPercent,
                WindowDays = windowDays
            };
        }
        catch
        {
            return new OptimizationEvidenceSnapshot();
        }
    }

    public static string BuildSummary(string key, string? evidenceJson)
    {
        var snapshot = Parse(evidenceJson);

        if (key.Equals("UnderutilizedSeats", StringComparison.OrdinalIgnoreCase))
        {
            if (snapshot.SeatsPurchased.HasValue && snapshot.PeakUsed.HasValue)
            {
                var seats = snapshot.SeatsPurchased.Value;
                var peak = snapshot.PeakUsed.Value;
                var percent = snapshot.UtilizationPercent
                              ?? (seats > 0 ? Math.Round(peak * 100d / seats, 1) : (double?)null);
                var percentLabel = percent.HasValue ? $"{percent.Value:0.#}%" : "?";
                var windowLabel = snapshot.WindowDays.HasValue ? $"{snapshot.WindowDays.Value}d" : "?";
                return $"Utilization {percentLabel} (peak {peak} of {seats} seats over {windowLabel}).";
            }
        }

        if (key.Equals("UnassignedSeats", StringComparison.OrdinalIgnoreCase))
        {
            if (snapshot.Unassigned.HasValue && snapshot.SeatsPurchased.HasValue)
            {
                var assignedLabel = snapshot.SeatsAssigned.HasValue ? snapshot.SeatsAssigned.Value.ToString() : "?";
                return $"Unassigned {snapshot.Unassigned.Value} of {snapshot.SeatsPurchased.Value} seats (assigned {assignedLabel}).";
            }
        }

        var parts = new List<string>();
        if (snapshot.SeatsPurchased.HasValue)
        {
            parts.Add($"Seats purchased: {snapshot.SeatsPurchased.Value}");
        }

        if (snapshot.SeatsAssigned.HasValue)
        {
            parts.Add($"Seats assigned: {snapshot.SeatsAssigned.Value}");
        }

        if (snapshot.PeakUsed.HasValue)
        {
            parts.Add($"Peak used: {snapshot.PeakUsed.Value}");
        }

        if (snapshot.Unassigned.HasValue)
        {
            parts.Add($"Unassigned: {snapshot.Unassigned.Value}");
        }

        if (snapshot.WindowDays.HasValue)
        {
            parts.Add($"Window: {snapshot.WindowDays.Value}d");
        }

        return parts.Count > 0 ? string.Join(" ", parts) : "Evidence details unavailable.";
    }
}

public sealed class OptimizationEvidenceSnapshot
{
    public int? SeatsPurchased { get; init; }
    public int? SeatsAssigned { get; init; }
    public int? Unassigned { get; init; }
    public int? PeakUsed { get; init; }
    public double? UtilizationPercent { get; init; }
    public int? WindowDays { get; init; }
}
