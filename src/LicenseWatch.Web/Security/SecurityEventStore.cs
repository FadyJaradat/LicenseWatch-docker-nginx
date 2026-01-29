namespace LicenseWatch.Web.Security;

public sealed class SecurityEventStore : ISecurityEventStore
{
    private readonly object _sync = new();
    private readonly Queue<SecurityEvent> _events = new();
    private readonly int _capacity;

    public SecurityEventStore(int capacity = 200)
    {
        _capacity = Math.Max(50, capacity);
    }

    public void Add(SecurityEvent entry)
    {
        lock (_sync)
        {
            _events.Enqueue(entry);
            while (_events.Count > _capacity)
            {
                _events.Dequeue();
            }
        }
    }

    public IReadOnlyList<SecurityEvent> GetRecent(int maxCount)
    {
        lock (_sync)
        {
            return _events.Reverse().Take(Math.Max(1, maxCount)).ToList();
        }
    }
}
