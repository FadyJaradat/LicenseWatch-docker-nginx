namespace LicenseWatch.Web.Diagnostics;

public enum StartupConfigSeverity
{
    Warning,
    Error
}

public sealed record StartupConfigIssue(string Key, string Message, StartupConfigSeverity Severity);

public sealed class StartupConfigState
{
    private readonly List<StartupConfigIssue> _issues = new();

    public IReadOnlyList<StartupConfigIssue> Issues => _issues;

    public bool HasErrors => _issues.Any(issue => issue.Severity == StartupConfigSeverity.Error);

    public void Add(string key, string message, StartupConfigSeverity severity)
    {
        _issues.Add(new StartupConfigIssue(key, message, severity));
    }
}
