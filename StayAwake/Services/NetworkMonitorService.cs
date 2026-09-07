using System.Net.NetworkInformation;
using StayAwake.Helpers;
using StayAwake.Models;

namespace StayAwake.Services;

public sealed class NetworkMonitorService
{
    private long _prevBytes;
    private DateTime _prevSampleUtc = DateTime.MinValue;
    private DateTime? _lastActiveUtc;
    private double _lastBytesPerSec;

    public double LastBytesPerSec => _lastBytesPerSec;

    public RuleResult Evaluate(long thresholdBytesPerSec, int graceSeconds, DateTime utcNow)
    {
        var bps = SampleBytesPerSec(utcNow);
        _lastBytesPerSec = bps;
        bool signal = bps >= thresholdBytesPerSec;
        bool active = GracePeriodTracker.Update(signal, utcNow, ref _lastActiveUtc, graceSeconds);

        if (active)
            return RuleResult.Matched("Network active", $"{FormatRate(bps)} ≥ {FormatRate(thresholdBytesPerSec)}");

        return RuleResult.NotMatched("Network idle", FormatRate(bps));
    }

    public double SampleBytesPerSec(DateTime utcNow)
    {
        long total = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                var stats = nic.GetIPv4Statistics();
                total += stats.BytesReceived + stats.BytesSent;
            }
        }
        catch
        {
            return _lastBytesPerSec;
        }

        if (_prevSampleUtc == DateTime.MinValue)
        {
            _prevBytes = total;
            _prevSampleUtc = utcNow;
            return 0;
        }

        var elapsed = (utcNow - _prevSampleUtc).TotalSeconds;
        if (elapsed <= 0.1)
            return _lastBytesPerSec;

        long delta = total - _prevBytes;
        if (delta < 0) delta = 0;

        _prevBytes = total;
        _prevSampleUtc = utcNow;
        return delta / elapsed;
    }

    public static string FormatRate(double bytesPerSec)
    {
        if (bytesPerSec >= 1_000_000)
            return $"{bytesPerSec / 1_000_000:F1} MB/s";
        if (bytesPerSec >= 1_000)
            return $"{bytesPerSec / 1_000:F1} KB/s";
        return $"{bytesPerSec:F0} B/s";
    }
}
