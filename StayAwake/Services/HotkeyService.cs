using System.Windows;
using System.Windows.Interop;
using StayAwake.Helpers;
using StayAwake.Interop;

namespace StayAwake.Services;

public sealed class HotkeyService : IDisposable
{
    public const int IdToggle = 1;
    public const int IdDisplay = 2;
    public const int IdPause = 3;

    private readonly LoggingService _log;
    private HwndSource? _source;
    private bool _toggleRegistered;
    private bool _displayRegistered;
    private bool _pauseRegistered;
    private bool _disposed;

    public event Action? TogglePressed;
    public event Action? DisplayPressed;
    public event Action? PausePressed;

    public string? LastError { get; private set; }

    public HotkeyService(LoggingService log) => _log = log;

    public void Attach(Window window)
    {
        var helper = new WindowInteropHelper(window);
        helper.EnsureHandle();
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);
    }

    public void RegisterAll(string toggleHotkey, string displayHotkey, bool enabled, string? pauseHotkey = null)
    {
        UnregisterAll();
        LastError = null;
        if (!enabled || _source == null)
            return;

        _toggleRegistered = TryRegister(IdToggle, toggleHotkey);
        _displayRegistered = TryRegister(IdDisplay, displayHotkey);
        if (!string.IsNullOrWhiteSpace(pauseHotkey))
            _pauseRegistered = TryRegister(IdPause, pauseHotkey!);

        if (!_toggleRegistered || !_displayRegistered)
            LastError ??= "Some hotkeys failed to register (conflict?)";
    }

    private bool TryRegister(int id, string hotkey)
    {
        if (!HotkeyParser.TryParse(hotkey, out uint mods, out uint vk, out string error))
        {
            LastError = $"Invalid hotkey '{hotkey}': {error}";
            _log.Warn(LastError);
            return false;
        }

        if (!HotkeyInterop.RegisterHotKey(_source!.Handle, id, mods, vk))
        {
            LastError = $"Hotkey '{hotkey}' is already in use";
            _log.Warn(LastError);
            return false;
        }

        _log.Info($"Registered hotkey {hotkey} as id={id}");
        return true;
    }

    public void UnregisterAll()
    {
        if (_source == null)
            return;
        if (_toggleRegistered)
        {
            HotkeyInterop.UnregisterHotKey(_source.Handle, IdToggle);
            _toggleRegistered = false;
        }
        if (_displayRegistered)
        {
            HotkeyInterop.UnregisterHotKey(_source.Handle, IdDisplay);
            _displayRegistered = false;
        }
        if (_pauseRegistered)
        {
            HotkeyInterop.UnregisterHotKey(_source.Handle, IdPause);
            _pauseRegistered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyInterop.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == IdToggle)
            {
                TogglePressed?.Invoke();
                handled = true;
            }
            else if (id == IdDisplay)
            {
                DisplayPressed?.Invoke();
                handled = true;
            }
            else if (id == IdPause)
            {
                PausePressed?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source = null;
    }
}
