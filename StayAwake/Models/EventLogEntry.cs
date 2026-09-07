namespace StayAwake.Models;

public sealed class EventLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Category { get; init; } = "Info";
    public string Message { get; init; } = string.Empty;
}
