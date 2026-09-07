using StayAwake.Helpers;
using StayAwake.Interop;
using StayAwake.Models;
using static StayAwake.Interop.NativeMethods;

namespace StayAwake.Services;

public sealed class CpuMonitorService
{
    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private bool _hasPrev;
    private DateTime? _lastActiveUtc;
    private double _lastPercent;

    public double LastPercent => _lastPercent;

    public RuleResult Evaluate(double thresholdPercent, int graceSeconds, DateTime utcNow)
    {
        var percent = SampleCpuPercent();
        _lastPercent = percent;
        bool signal = percent >= thresholdPercent;
        bool active = GracePeriodTracker.Update(signal, utcNow, ref _lastActiveUtc, graceSeconds);

        if (active)
            return RuleResult.Matched("CPU high", $"{percent:F1}% ≥ {thresholdPercent:F0}%");

        return RuleResult.NotMatched("CPU low", $"{percent:F1}%");
    }

    public double SampleCpuPercent()
    {
        if (!GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
            return _lastPercent;

        ulong idle = ToUInt64(idleFt);
        ulong kernel = ToUInt64(kernelFt);
        ulong user = ToUInt64(userFt);

        if (!_hasPrev)
        {
            _prevIdle = idle;
            _prevKernel = kernel;
            _prevUser = user;
            _hasPrev = true;
            return 0;
        }

        ulong idleDelta = idle - _prevIdle;
        ulong kernelDelta = kernel - _prevKernel;
        ulong userDelta = user - _prevUser;
        ulong total = kernelDelta + userDelta;

        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;

        if (total == 0)
            return _lastPercent;

        // kernel includes idle
        double busy = total - idleDelta;
        if (busy < 0) busy = 0;
        return Math.Clamp(100.0 * busy / total, 0, 100);
    }

    private static ulong ToUInt64(FILETIME ft) =>
        ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
}
