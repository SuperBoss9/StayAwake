using Microsoft.Win32;
using System.IO;

namespace StayAwake.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "StayAwake";
    private readonly LoggingService _log;

    public StartupService(LoggingService log) => _log = log;

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(ValueName) is string;
        }
        catch (Exception ex)
        {
            _log.Warn($"Startup check failed: {ex.Message}");
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled)
            {
                var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "StayAwake.exe");
                key.SetValue(ValueName, $"\"{exe}\" --minimized");
                _log.Info("Autostart enabled");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                _log.Info("Autostart disabled");
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to update autostart", ex);
        }
    }
}
