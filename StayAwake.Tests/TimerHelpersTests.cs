using StayAwake.Helpers;

namespace StayAwake.Tests;

public class TimerHelpersTests
{
    [Fact]
    public void CalculateTimedEndUtc_AddsMinutes()
    {
        var now = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        var end = TimerHelpers.CalculateTimedEndUtc(now, 90);
        Assert.Equal(now.AddMinutes(90), end);
    }

    [Fact]
    public void CalculateTimedEndUtc_ClampsMinimumToOne()
    {
        var now = DateTime.UtcNow;
        var end = TimerHelpers.CalculateTimedEndUtc(now, 0);
        Assert.Equal(now.AddMinutes(1), end);
    }

    [Fact]
    public void IsTimedActive_TrueWhenFuture()
    {
        var now = DateTime.UtcNow;
        Assert.True(TimerHelpers.IsTimedActive(now.AddMinutes(5), now));
        Assert.False(TimerHelpers.IsTimedActive(now.AddMinutes(-1), now));
        Assert.False(TimerHelpers.IsTimedActive(null, now));
    }

    [Fact]
    public void Remaining_ReturnsNullWhenExpired()
    {
        var now = DateTime.UtcNow;
        Assert.Null(TimerHelpers.Remaining(now.AddSeconds(-1), now));
        var rem = TimerHelpers.Remaining(now.AddMinutes(10), now);
        Assert.NotNull(rem);
        Assert.True(rem!.Value.TotalMinutes > 9);
    }

    [Fact]
    public void Until_UsesTodayWhenFuture()
    {
        var local = new DateTime(2026, 9, 6, 10, 0, 0);
        var end = TimerHelpers.CalculateUntilEndLocal(local, "22:00");
        Assert.Equal(new DateTime(2026, 9, 6, 22, 0, 0), end);
    }

    [Fact]
    public void Until_RollsToNextDayWhenPast()
    {
        var local = new DateTime(2026, 9, 6, 23, 0, 0);
        var end = TimerHelpers.CalculateUntilEndLocal(local, "22:00");
        Assert.Equal(new DateTime(2026, 9, 7, 22, 0, 0), end);
    }

    [Fact]
    public void SanitizePersistedEnd_DropsExpired()
    {
        var now = DateTime.UtcNow;
        Assert.Null(TimerHelpers.SanitizePersistedEnd(now.AddMinutes(-5), now));
        var future = now.AddHours(1);
        Assert.Equal(future, TimerHelpers.SanitizePersistedEnd(future, now));
    }

    [Theory]
    [InlineData("9:30", true)]
    [InlineData("09:30", true)]
    [InlineData("22:00:00", true)]
    [InlineData("bad", false)]
    [InlineData("", false)]
    public void TryParseTime(string text, bool ok)
    {
        Assert.Equal(ok, TimerHelpers.TryParseTime(text, out _));
    }
}
