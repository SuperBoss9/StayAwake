using StayAwake.Helpers;

namespace StayAwake.Tests;

public class GracePeriodTrackerTests
{
    [Fact]
    public void SignalTrue_ActivatesAndStores()
    {
        DateTime? last = null;
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(GracePeriodTracker.Update(true, now, ref last, 30));
        Assert.Equal(now, last);
    }

    [Fact]
    public void AfterSignal_StaysTrueDuringGrace()
    {
        DateTime? last = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var now = last.Value.AddSeconds(10);
        Assert.True(GracePeriodTracker.Update(false, now, ref last, 30));
    }

    [Fact]
    public void AfterGrace_BecomesFalse()
    {
        DateTime? last = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var now = last.Value.AddSeconds(31);
        Assert.False(GracePeriodTracker.Update(false, now, ref last, 30));
    }

    [Fact]
    public void NeverActive_False()
    {
        DateTime? last = null;
        Assert.False(GracePeriodTracker.Update(false, DateTime.UtcNow, ref last, 30));
    }

    [Fact]
    public void IsWithinGrace_Helper()
    {
        var t = DateTime.UtcNow;
        Assert.True(GracePeriodTracker.IsWithinGrace(t, t.AddSeconds(5), 10));
        Assert.False(GracePeriodTracker.IsWithinGrace(t, t.AddSeconds(15), 10));
        Assert.False(GracePeriodTracker.IsWithinGrace(null, t, 10));
    }
}
