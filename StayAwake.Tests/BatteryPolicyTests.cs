using StayAwake.Helpers;

namespace StayAwake.Tests;

public class BatteryPolicyTests
{
    [Fact]
    public void AcPower_NeverBlocks()
    {
        Assert.False(BatteryPolicy.ShouldBlock(false, 5, false, true, 20));
    }

    [Fact]
    public void DisallowOnBattery_Blocks()
    {
        Assert.True(BatteryPolicy.ShouldBlock(true, 100, allowOnBattery: false, disableBelowPercent: false, cutoffPercent: 15));
    }

    [Fact]
    public void BelowCutoff_Blocks()
    {
        Assert.True(BatteryPolicy.ShouldBlock(true, 10, true, true, 15));
        Assert.False(BatteryPolicy.ShouldBlock(true, 20, true, true, 15));
    }

    [Fact]
    public void CutoffDisabled_IgnoresPercent()
    {
        Assert.False(BatteryPolicy.ShouldBlock(true, 5, true, disableBelowPercent: false, cutoffPercent: 50));
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(150, 100)]
    [InlineData(42, 42)]
    public void ClampPercent(int input, int expected) =>
        Assert.Equal(expected, BatteryPolicy.ClampPercent(input));
}
