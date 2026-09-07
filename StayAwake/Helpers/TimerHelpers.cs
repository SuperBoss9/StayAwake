namespace StayAwake.Helpers;

public static class TimerHelpers
{
    public static DateTime CalculateTimedEndUtc(DateTime utcNow, int durationMinutes)
    {
        if (durationMinutes < 1)
            durationMinutes = 1;
        return utcNow.AddMinutes(durationMinutes);
    }

    public static bool IsTimedActive(DateTime? endUtc, DateTime utcNow) =>
        endUtc.HasValue && endUtc.Value > utcNow;

    public static TimeSpan? Remaining(DateTime? endUtc, DateTime utcNow)
    {
        if (!endUtc.HasValue || endUtc.Value <= utcNow)
            return null;
        return endUtc.Value - utcNow;
    }

    /// <summary>
    /// Parses HH:mm or H:mm and returns the next local DateTime for that wall-clock time.
    /// If the time has already passed today, returns tomorrow.
    /// </summary>
    public static DateTime CalculateUntilEndLocal(DateTime localNow, string untilTime)
    {
        if (!TryParseTime(untilTime, out var time))
            time = new TimeSpan(22, 0, 0);

        var candidate = localNow.Date.Add(time);
        if (candidate <= localNow)
            candidate = candidate.AddDays(1);
        return candidate;
    }

    public static DateTime CalculateUntilEndUtc(DateTime utcNow, DateTime localNow, string untilTime)
    {
        var localEnd = CalculateUntilEndLocal(localNow, untilTime);
        var offset = localNow - utcNow;
        return localEnd - offset;
    }

    public static bool TryParseTime(string text, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        return TimeSpan.TryParseExact(text.Trim(), new[] { @"h\:mm", @"hh\:mm", @"h\:mm\:ss", @"hh\:mm\:ss" },
            null, out time);
    }

    /// <summary>After restart: discard expired timers; keep only future ends.</summary>
    public static DateTime? SanitizePersistedEnd(DateTime? endUtc, DateTime utcNow) =>
        endUtc.HasValue && endUtc.Value > utcNow ? endUtc : null;
}
