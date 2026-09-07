using StayAwake.Helpers;
using StayAwake.Models;

namespace StayAwake.Tests;

public class ScheduleHelpersTests
{
    private static ScheduleRuleModel Rule(TimeSpan start, TimeSpan end, int days, bool enabled = true) =>
        new() { StartTime = start, EndTime = end, DaysOfWeek = days, Enabled = enabled, Name = "t" };

    [Fact]
    public void SameDayWindow_MatchesInside()
    {
        // Monday
        var rule = Rule(new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), 1 << (int)DayOfWeek.Monday);
        var now = new DateTime(2026, 9, 7, 12, 0, 0); // Monday
        Assert.Equal(DayOfWeek.Monday, now.DayOfWeek);
        Assert.True(ScheduleHelpers.IsWithinSchedule(rule, now));
    }

    [Fact]
    public void SameDayWindow_RejectsOutside()
    {
        var rule = Rule(new TimeSpan(9, 0, 0), new TimeSpan(18, 0, 0), 1 << (int)DayOfWeek.Monday);
        var now = new DateTime(2026, 9, 7, 20, 0, 0);
        Assert.False(ScheduleHelpers.IsWithinSchedule(rule, now));
    }

    [Fact]
    public void Overnight_ActiveAfterStart()
    {
        var rule = Rule(new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), 1 << (int)DayOfWeek.Monday);
        var now = new DateTime(2026, 9, 7, 23, 0, 0); // Mon 23:00
        Assert.True(ScheduleHelpers.IsWithinSchedule(rule, now));
    }

    [Fact]
    public void Overnight_ActiveBeforeEndNextMorning()
    {
        var rule = Rule(new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), 1 << (int)DayOfWeek.Monday);
        var now = new DateTime(2026, 9, 8, 5, 0, 0); // Tue 05:00 continuing from Mon
        Assert.True(ScheduleHelpers.IsWithinSchedule(rule, now));
    }

    [Fact]
    public void Overnight_InactiveMidDay()
    {
        var rule = Rule(new TimeSpan(22, 0, 0), new TimeSpan(6, 0, 0), 1 << (int)DayOfWeek.Monday);
        var now = new DateTime(2026, 9, 8, 12, 0, 0);
        Assert.False(ScheduleHelpers.IsWithinSchedule(rule, now));
    }

    [Fact]
    public void DisabledRule_NeverMatches()
    {
        var rule = Rule(new TimeSpan(0, 0, 0), new TimeSpan(23, 59, 0), 0b1111111, enabled: false);
        Assert.False(ScheduleHelpers.IsWithinSchedule(rule, DateTime.Now));
    }
}
