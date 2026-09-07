using System.Windows.Forms;

namespace StayAwake.Services;

public sealed class NotificationService
{
    private readonly TrayService _tray;
    private readonly LoggingService _log;

    public NotificationService(TrayService tray, LoggingService log)
    {
        _tray = tray;
        _log = log;
    }

    public void Show(string title, string message, bool enabled)
    {
        if (!enabled)
            return;

        try
        {
            // Prefer balloon via NotifyIcon (works without extra packages / WinRT toast deps)
            _tray.ShowBalloon(title, message, ToolTipIcon.Info);
            _log.Info($"Notify: {title} — {message}");
        }
        catch (Exception ex)
        {
            _log.Warn($"Notification failed: {ex.Message}");
        }
    }
}
