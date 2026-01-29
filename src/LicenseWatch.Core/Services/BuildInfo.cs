using System.Globalization;
using System.Reflection;

namespace LicenseWatch.Core.Services;

public static class BuildInfo
{
    private static Assembly Assembly
        => Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

    public static string InformationalVersion => Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public static string Version => ExtractVersion(InformationalVersion);
    public static string DisplayVersion => $"v{Version}";
    public static string? Commit => ExtractCommit(InformationalVersion);
    public static string? CommitShort => Commit is null
        ? null
        : Commit.Length > 8 ? Commit[..8] : Commit;
    public static DateTime? BuildTimestampUtc => ReadBuildTimestamp();

    public static string BuildTimestampDisplay => BuildTimestampUtc.HasValue
        ? BuildTimestampUtc.Value.ToLocalTime().ToString("g")
        : "Unknown";

    private static DateTime? ReadBuildTimestamp()
    {
        var value = Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "BuildTimestamp")?.Value;

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return null;
    }

    private static string ExtractVersion(string version)
    {
        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex > 0)
        {
            return version[..plusIndex];
        }

        return version;
    }

    private static string? ExtractCommit(string version)
    {
        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex < 0 || plusIndex >= version.Length - 1)
        {
            return null;
        }

        return version[(plusIndex + 1)..];
    }
}
