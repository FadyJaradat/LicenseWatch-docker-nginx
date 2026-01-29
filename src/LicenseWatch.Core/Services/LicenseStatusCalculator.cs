namespace LicenseWatch.Core.Services;

public static class LicenseStatusCalculator
{
    public const int DefaultCriticalDays = 30;
    public const int DefaultWarningDays = 90;

    public static string ComputeStatus(DateTime? expiresOnUtc, DateTime? nowUtc = null)
        => ComputeStatus(expiresOnUtc, DefaultCriticalDays, DefaultWarningDays, nowUtc);

    public static string ComputeStatus(DateTime? expiresOnUtc, int criticalDays, int warningDays, DateTime? nowUtc = null)
    {
        if (!expiresOnUtc.HasValue)
        {
            return "Unknown";
        }

        var now = (nowUtc ?? DateTime.UtcNow).Date;
        var thresholds = NormalizeThresholds(criticalDays, warningDays);
        var days = (expiresOnUtc.Value.Date - now).TotalDays;
        if (days <= 0)
        {
            return "Expired";
        }

        if (days <= thresholds.CriticalDays)
        {
            return "Critical";
        }

        if (days <= thresholds.WarningDays)
        {
            return "Warning";
        }

        return "Good";
    }

    public static (int CriticalDays, int WarningDays) NormalizeThresholds(int criticalDays, int warningDays)
    {
        var critical = criticalDays <= 0 ? DefaultCriticalDays : criticalDays;
        var warning = warningDays <= 0 ? DefaultWarningDays : warningDays;
        if (warning <= critical)
        {
            warning = critical + 1;
        }

        return (critical, warning);
    }
}
