using StayAwake.Models;

namespace StayAwake.Helpers;

public static class ScheduleHelpers
{
    public static bool IsDayEnabled(int daysOfWeekFlags, DayOfWeek day) =>
        (daysOfWeekFlags & (1 << (int)day)) != 0;

    /// <summary>
    /// Returns true when localNow falls within the schedule window.
    /// Overnight spans (e.g. 22:00–06:00) are supported: active from start until midnight
    /// on start day, and from midnight until end on the following day.
    /// </summary>
    public static bool IsWithinSchedule(ScheduleRuleModel rule, DateTime localNow)
    {
        if (!rule.Enabled)
            return false;

        var time = localNow.TimeOfDay;
        var today = localNow.DayOfWeek;
        var yesterday = localNow.AddDays(-1).DayOfWeek;

        bool overnight = rule.EndTime <= rule.StartTime;

        if (!overnight)
        {
            return IsDayEnabled(rule.DaysOfWeek, today)
                   && time >= rule.StartTime
                   && time < rule.EndTime;
        }

        // After start time today
        if (IsDayEnabled(rule.DaysOfWeek, today) && time >= rule.StartTime)
            return true;

        // Before end time, continuing from yesterday
        if (IsDayEnabled(rule.DaysOfWeek, yesterday) && time < rule.EndTime)
            return true;

        return false;
    }

    public static bool AnyScheduleActive(IEnumerable<ScheduleRuleModel> rules, DateTime localNow) =>
        rules.Any(r => IsWithinSchedule(r, localNow));
}
