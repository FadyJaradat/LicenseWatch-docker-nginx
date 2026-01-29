namespace LicenseWatch.Web.Diagnostics;

public sealed class AppRuntimeInfo
{
    public AppRuntimeInfo(DateTime startedAtUtc)
    {
        StartedAtUtc = startedAtUtc;
    }

    public DateTime StartedAtUtc { get; }
}
