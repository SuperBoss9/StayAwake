using System.Runtime.InteropServices;
using static StayAwake.Interop.NativeMethods;

namespace StayAwake.Interop;

/// <summary>Thin wrapper around Power Request API and SetThreadExecutionState fallback.</summary>
public sealed class PowerInterop : IDisposable
{
    private IntPtr _requestHandle = IntPtr.Zero;
    private IntPtr _reasonStringPtr = IntPtr.Zero;
    private bool _systemSet;
    private bool _displaySet;
    private bool _useFallback;
    private bool _disposed;

    public bool UsingFallback => _useFallback;
    public bool IsActive => _systemSet || _displaySet;

    public void Apply(bool systemRequired, bool displayRequired)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!systemRequired && !displayRequired)
        {
            Release();
            return;
        }

        EnsureRequestCreated();

        if (_useFallback)
        {
            ApplyFallback(systemRequired, displayRequired);
            return;
        }

        SetPowerRequest(POWER_REQUEST_TYPE.PowerRequestSystemRequired, systemRequired, ref _systemSet);
        SetPowerRequest(POWER_REQUEST_TYPE.PowerRequestDisplayRequired, displayRequired, ref _displaySet);

        // Also set ES_CONTINUOUS fallback path soft-sync if power request partially failed
        if (!_systemSet && systemRequired)
        {
            _useFallback = true;
            ApplyFallback(systemRequired, displayRequired);
        }
    }

    public void Release()
    {
        if (_useFallback)
        {
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
            _systemSet = false;
            _displaySet = false;
            return;
        }

        if (_requestHandle == IntPtr.Zero)
            return;

        if (_systemSet)
        {
            PowerClearRequest(_requestHandle, POWER_REQUEST_TYPE.PowerRequestSystemRequired);
            _systemSet = false;
        }

        if (_displaySet)
        {
            PowerClearRequest(_requestHandle, POWER_REQUEST_TYPE.PowerRequestDisplayRequired);
            _displaySet = false;
        }
    }

    private void EnsureRequestCreated()
    {
        if (_requestHandle != IntPtr.Zero || _useFallback)
            return;

        _reasonStringPtr = Marshal.StringToHGlobalUni("StayAwake");
        var context = new REASON_CONTEXT
        {
            Version = POWER_REQUEST_CONTEXT_VERSION,
            Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING,
            SimpleReasonString = _reasonStringPtr
        };

        _requestHandle = PowerCreateRequest(ref context);
        if (_requestHandle == IntPtr.Zero)
        {
            _useFallback = true;
            if (_reasonStringPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_reasonStringPtr);
                _reasonStringPtr = IntPtr.Zero;
            }
        }
    }

    private void SetPowerRequest(POWER_REQUEST_TYPE type, bool enable, ref bool current)
    {
        if (enable == current)
            return;

        bool ok = enable
            ? PowerSetRequest(_requestHandle, type)
            : PowerClearRequest(_requestHandle, type);

        if (ok)
            current = enable;
        else if (enable)
            _useFallback = true;
    }

    private void ApplyFallback(bool systemRequired, bool displayRequired)
    {
        var flags = EXECUTION_STATE.ES_CONTINUOUS;
        if (systemRequired)
            flags |= EXECUTION_STATE.ES_SYSTEM_REQUIRED;
        if (displayRequired)
            flags |= EXECUTION_STATE.ES_DISPLAY_REQUIRED;

        SetThreadExecutionState(flags);
        _systemSet = systemRequired;
        _displaySet = displayRequired;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Release();
        if (_requestHandle != IntPtr.Zero)
        {
            CloseHandle(_requestHandle);
            _requestHandle = IntPtr.Zero;
        }
        if (_reasonStringPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_reasonStringPtr);
            _reasonStringPtr = IntPtr.Zero;
        }
    }
}
