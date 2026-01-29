namespace LicenseWatch.Web.Models.Help;

public sealed class ReleaseNotesViewModel
{
    public IReadOnlyList<ReleaseNotesVersion> Versions { get; init; } = Array.Empty<ReleaseNotesVersion>();
}

public sealed class ReleaseNotesVersion
{
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string DateLabel { get; init; } = string.Empty;
    public bool IsCurrent { get; init; }
    public IReadOnlyList<ReleaseNotesSection> Sections { get; init; } = Array.Empty<ReleaseNotesSection>();
}

public sealed class ReleaseNotesSection
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> Items { get; init; } = Array.Empty<string>();
}
