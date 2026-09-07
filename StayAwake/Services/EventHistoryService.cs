using StayAwake.Models;

namespace StayAwake.Services;

public sealed class EventHistoryService
{
    private readonly object _sync = new();
    private readonly List<EventLogEntry> _entries = new();
    private const int MaxEntries = 50;

    public event Action? Changed;

    public IReadOnlyList<EventLogEntry> GetEntries()
    {
        lock (_sync)
            return _entries.ToList();
    }

    public void Add(string category, string message)
    {
        lock (_sync)
        {
            _entries.Insert(0, new EventLogEntry
            {
                Timestamp = DateTime.Now,
                Category = category,
                Message = message
            });
            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(_entries.Count - 1);
        }
        Changed?.Invoke();
    }
}
