namespace LicenseWatch.Web.Security;

public sealed record SecurityEvent(
    DateTime OccurredAtUtc,
    string EventType,
    string Summary,
    string? Path,
    string? IpAddress,
    string? UserEmail);
