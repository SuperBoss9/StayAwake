using StayAwake.Interop;

namespace StayAwake.Services;

public sealed class PowerManager : IDisposable
{
    private readonly LoggingService _log;
    private readonly PowerInterop _power = new();
    private readonly object _sync = new();
    private bool _system;
    private bool _display;
    private bool _disposed;

    public PowerManager(LoggingService log) => _log = log;

    public bool IsSystemRequired
    {
        get { lock (_sync) return _system; }
    }

    public bool IsDisplayRequired
    {
        get { lock (_sync) return _display; }
    }

    public bool UsingFallback => _power.UsingFallback;

    public void Apply(bool systemRequired, bool displayRequired)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_system == systemRequired && _display == displayRequired)
                return;

            try
            {
                _power.Apply(systemRequired, displayRequired);
                _system = systemRequired;
                _display = displayRequired;
                _log.Info($"Power Apply system={systemRequired} display={displayRequired} fallback={_power.UsingFallback}");
            }
            catch (Exception ex)
            {
                _log.Error("Power Apply failed", ex);
            }
        }
    }

    public void Release()
    {
        lock (_sync)
        {
            if (!_system && !_display)
                return;
            try
            {
                _power.Release();
                _system = false;
                _display = false;
                _log.Info("Power released");
            }
            catch (Exception ex)
            {
                _log.Error("Power Release failed", ex);
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
            try { _power.Dispose(); } catch { /* ignore */ }
            _system = false;
            _display = false;
        }
    }
}
