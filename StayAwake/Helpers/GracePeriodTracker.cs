namespace StayAwake.Helpers;

public static class GracePeriodTracker
{
    /// <summary>
    /// Pure grace-period logic: once the signal goes true, stay true until
    /// the signal has been false continuously for <paramref name="graceSeconds"/>.
    /// </summary>
    public static bool Update(
        bool signalActive,
        DateTime utcNow,
        ref DateTime? lastActiveUtc,
        int graceSeconds)
    {
        if (graceSeconds < 0)
            graceSeconds = 0;

        if (signalActive)
        {
            lastActiveUtc = utcNow;
            return true;
        }

        if (!lastActiveUtc.HasValue)
            return false;

        var idle = utcNow - lastActiveUtc.Value;
        if (idle.TotalSeconds < graceSeconds)
            return true;

        return false;
    }

    public static bool IsWithinGrace(DateTime? lastActiveUtc, DateTime utcNow, int graceSeconds)
    {
        if (!lastActiveUtc.HasValue)
            return false;
        return (utcNow - lastActiveUtc.Value).TotalSeconds < graceSeconds;
    }
}
