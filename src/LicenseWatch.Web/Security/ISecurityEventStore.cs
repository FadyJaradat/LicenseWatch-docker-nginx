namespace LicenseWatch.Web.Security;

public interface ISecurityEventStore
{
    void Add(SecurityEvent entry);
    IReadOnlyList<SecurityEvent> GetRecent(int maxCount);
}
