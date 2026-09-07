namespace StayAwake.Models;

public sealed class AwakeStateSnapshot
{
    public bool IsAwakeActive { get; init; }
    public bool IsPaused { get; init; }
    public bool KeepDisplayOn { get; init; }
    public AwakeMode Mode { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public string ReasonsText { get; init; } = string.Empty;
    public DateTime? EndsAtLocal { get; init; }
    public TimeSpan? Remaining { get; init; }
    public bool BatteryBlocked { get; init; }
    public int BatteryPercent { get; init; }
    public bool OnBattery { get; init; }
    public IReadOnlyList<RuleResult> RuleResults { get; init; } = Array.Empty<RuleResult>();

    public string FormatRemaining()
    {
        if (!Remaining.HasValue || Remaining.Value <= TimeSpan.Zero)
            return string.Empty;
        var r = Remaining.Value;
        if (r.TotalHours >= 1)
            return $"{(int)r.TotalHours}h {r.Minutes}m";
        if (r.TotalMinutes >= 1)
            return $"{r.Minutes}m {r.Seconds}s";
        return $"{r.Seconds}s";
    }
}
