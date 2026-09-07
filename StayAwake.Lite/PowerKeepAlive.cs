using System.Runtime.InteropServices;

namespace StayAwake.Lite;

/// <summary>Keeps the system (and optionally display) awake via Power Request API.</summary>
internal sealed class PowerKeepAlive : IDisposable
{
    private const uint PowerRequestContextVersion = 0;
    private const uint PowerRequestContextSimpleString = 0x1;

    private enum PowerRequestType
    {
        DisplayRequired = 0,
        SystemRequired = 1
    }

    [Flags]
    private enum ExecutionState : uint
    {
        SystemRequired = 0x00000001,
        DisplayRequired = 0x00000002,
        Continuous = 0x80000000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ReasonContext
    {
        public uint Version;
        public uint Flags;
        public IntPtr SimpleReasonString;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr PowerCreateRequest(ref ReasonContext context);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerSetRequest(IntPtr powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PowerClearRequest(IntPtr powerRequest, PowerRequestType requestType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState flags);

    private IntPtr _request = IntPtr.Zero;
    private IntPtr _reasonPtr = IntPtr.Zero;
    private bool _system;
    private bool _display;
    private bool _fallback;
    private bool _disposed;

    public void Apply(bool keepDisplayOn)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureCreated();

        if (_fallback)
        {
            ApplyFallback(keepDisplayOn);
            return;
        }

        Set(PowerRequestType.SystemRequired, true, ref _system);
        Set(PowerRequestType.DisplayRequired, keepDisplayOn, ref _display);

        if (!_system)
        {
            _fallback = true;
            ApplyFallback(keepDisplayOn);
        }
    }

    public void Release()
    {
        if (_fallback)
        {
            SetThreadExecutionState(ExecutionState.Continuous);
            _system = false;
            _display = false;
            return;
        }

        if (_request == IntPtr.Zero)
            return;

        if (_system)
        {
            PowerClearRequest(_request, PowerRequestType.SystemRequired);
            _system = false;
        }

        if (_display)
        {
            PowerClearRequest(_request, PowerRequestType.DisplayRequired);
            _display = false;
        }
    }

    private void EnsureCreated()
    {
        if (_request != IntPtr.Zero || _fallback)
            return;

        _reasonPtr = Marshal.StringToHGlobalUni("StayAwake.Lite");
        var ctx = new ReasonContext
        {
            Version = PowerRequestContextVersion,
            Flags = PowerRequestContextSimpleString,
            SimpleReasonString = _reasonPtr
        };

        _request = PowerCreateRequest(ref ctx);
        if (_request == IntPtr.Zero)
        {
            _fallback = true;
            if (_reasonPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_reasonPtr);
                _reasonPtr = IntPtr.Zero;
            }
        }
    }

    private void Set(PowerRequestType type, bool enable, ref bool current)
    {
        if (enable == current)
            return;

        bool ok = enable
            ? PowerSetRequest(_request, type)
            : PowerClearRequest(_request, type);

        if (ok)
            current = enable;
        else if (enable)
            _fallback = true;
    }

    private void ApplyFallback(bool keepDisplayOn)
    {
        var flags = ExecutionState.Continuous | ExecutionState.SystemRequired;
        if (keepDisplayOn)
            flags |= ExecutionState.DisplayRequired;
        SetThreadExecutionState(flags);
        _system = true;
        _display = keepDisplayOn;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Release();
        if (_request != IntPtr.Zero)
        {
            CloseHandle(_request);
            _request = IntPtr.Zero;
        }
        if (_reasonPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_reasonPtr);
            _reasonPtr = IntPtr.Zero;
        }
    }
}
