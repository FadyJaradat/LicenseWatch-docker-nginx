using LicenseWatch.Core.Services;

namespace LicenseWatch.Web;

public static class AppInfo
{
    public static string InformationalVersion => BuildInfo.InformationalVersion;
    public static string Version => BuildInfo.Version;
    public static string DisplayVersion => BuildInfo.DisplayVersion;
    public static string? Commit => BuildInfo.Commit;
    public static string? CommitShort => BuildInfo.CommitShort;
    public static DateTime? BuildTimestampUtc => BuildInfo.BuildTimestampUtc;
    public static string BuildTimestampDisplay => BuildInfo.BuildTimestampDisplay;
}
