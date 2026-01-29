namespace LicenseWatch.Infrastructure.Email;

public static class EmailNotificationCatalog
{
    public static readonly IReadOnlyList<EmailNotificationDefinition> Defaults = new List<EmailNotificationDefinition>
    {
        new("License.Created", "License created", "Instant"),
        new("License.Updated", "License updated", "Instant"),
        new("License.Deleted", "License deleted", "Instant"),
        new("Compliance.Changes", "Compliance breaches updated", "Daily"),
        new("Optimization.Insights", "Optimization insights updated", "Weekly")
    };

    public static IReadOnlyList<string> Frequencies { get; } = new List<string>
    {
        "Instant",
        "Daily",
        "Weekly"
    };
}

public record EmailNotificationDefinition(string EventKey, string Name, string Frequency);
