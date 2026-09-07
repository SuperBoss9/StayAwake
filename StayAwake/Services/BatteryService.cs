using StayAwake.Helpers;

namespace StayAwake.Services;

public sealed class BatteryService
{
    public bool OnBattery { get; private set; }
    public int Percent { get; private set; } = 100;

    public void Refresh()
    {
        try
        {
            var status = System.Windows.Forms.SystemInformation.PowerStatus;
            OnBattery = status.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline;
            var life = status.BatteryLifePercent;
            Percent = life < 0 || float.IsNaN(life)
                ? 100
                : BatteryPolicy.ClampPercent((int)Math.Round(life * 100));
        }
        catch
        {
            OnBattery = false;
            Percent = 100;
        }
    }

    public bool ShouldBlock(bool allowOnBattery, bool disableBelow, int cutoff)
    {
        Refresh();
        return BatteryPolicy.ShouldBlock(OnBattery, Percent, allowOnBattery, disableBelow, cutoff);
    }
}
