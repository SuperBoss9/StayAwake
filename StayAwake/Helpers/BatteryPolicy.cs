namespace StayAwake.Helpers;

public static class BatteryPolicy
{
    /// <summary>
    /// Returns true when power-request must be blocked due to battery policy.
    /// </summary>
    public static bool ShouldBlock(
        bool onBattery,
        int batteryPercent,
        bool allowOnBattery,
        bool disableBelowPercent,
        int cutoffPercent)
    {
        if (!onBattery)
            return false;

        if (!allowOnBattery)
            return true;

        if (disableBelowPercent && batteryPercent < cutoffPercent)
            return true;

        return false;
    }

    public static int ClampPercent(int percent) => Math.Clamp(percent, 0, 100);
}
