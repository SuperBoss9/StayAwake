using System.Drawing;
using System.Windows.Forms;
using StayAwake.Models;

namespace StayAwake.Services;

public enum TrayIconState
{
    Off,
    On,
    Battery,
    Error,
    Pause
}

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Dictionary<TrayIconState, Icon> _icons = new();
    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _displayItem;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? IndefiniteRequested;
    public event EventHandler<int>? TimedMinutesRequested;
    public event EventHandler? UntilRequested;
    public event EventHandler? ProcessPickerRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? ExitRequested;
    public event EventHandler<bool>? KeepDisplayChanged;

    public TrayService()
    {
        BuildIcons();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icons[TrayIconState.Off],
            Text = "StayAwake",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetState(TrayIconState state, string? tooltip = null)
    {
        if (_disposed) return;
        if (_icons.TryGetValue(state, out var icon))
            _notifyIcon.Icon = icon;
        if (!string.IsNullOrWhiteSpace(tooltip))
            _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];
    }

    public void UpdateStatus(AwakeStateSnapshot snap)
    {
        if (_disposed || _statusItem == null) return;

        string text;
        if (snap.IsPaused)
            text = "Paused";
        else if (!snap.IsAwakeActive)
            text = "Off";
        else if (snap.Remaining.HasValue && snap.Remaining.Value > TimeSpan.Zero)
            text = $"Active: {snap.FormatRemaining()} left";
        else
            text = $"Active: {snap.ReasonsText}";

        if (text.Length > 60)
            text = text[..57] + "...";
        _statusItem.Text = text;

        if (_displayItem != null)
            _displayItem.Checked = snap.KeepDisplayOn;
    }

    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_disposed) return;
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void UpdateLocalizedLabels(
        string open, string indefinite, string until, string process,
        string keepDisplay, string pause, string exit, string settings)
    {
        if (_notifyIcon.ContextMenuStrip is not { } menu) return;
        // Indices match BuildMenu order
        SetText(menu, 2, indefinite);
        SetText(menu, 9, until);
        SetText(menu, 10, process);
        if (_displayItem != null) _displayItem.Text = keepDisplay;
        SetText(menu, 13, open);
        SetText(menu, 14, settings);
        SetText(menu, 15, pause);
        SetText(menu, 17, exit);
    }

    private static void SetText(ContextMenuStrip menu, int index, string text)
    {
        if (index >= 0 && index < menu.Items.Count)
            menu.Items[index].Text = text;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("Off") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Indefinite", null, (_, _) => IndefiniteRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("30 min", null, (_, _) => TimedMinutesRequested?.Invoke(this, 30));
        menu.Items.Add("1 h", null, (_, _) => TimedMinutesRequested?.Invoke(this, 60));
        menu.Items.Add("2 h", null, (_, _) => TimedMinutesRequested?.Invoke(this, 120));
        menu.Items.Add("4 h", null, (_, _) => TimedMinutesRequested?.Invoke(this, 240));
        menu.Items.Add("8 h", null, (_, _) => TimedMinutesRequested?.Invoke(this, 480));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Until…", null, (_, _) => UntilRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Process…", null, (_, _) => ProcessPickerRequested?.Invoke(this, EventArgs.Empty));

        _displayItem = new ToolStripMenuItem("Keep display on")
        {
            CheckOnClick = true
        };
        _displayItem.CheckedChanged += (_, _) =>
        {
            if (_displayItem != null)
                KeepDisplayChanged?.Invoke(this, _displayItem.Checked);
        };
        menu.Items.Add(_displayItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Open", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Settings", null, (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Pause", null, (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        return menu;
    }

    private void BuildIcons()
    {
        _icons[TrayIconState.Off] = CreateIcon(Color.FromArgb(120, 120, 120), Color.White, "–");
        _icons[TrayIconState.On] = CreateIcon(Color.FromArgb(46, 160, 67), Color.White, "A");
        _icons[TrayIconState.Battery] = CreateIcon(Color.FromArgb(210, 140, 20), Color.White, "B");
        _icons[TrayIconState.Error] = CreateIcon(Color.FromArgb(200, 50, 50), Color.White, "!");
        _icons[TrayIconState.Pause] = CreateIcon(Color.FromArgb(70, 110, 180), Color.White, "P");
    }

    private static Icon CreateIcon(Color bg, Color fg, string letter)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(bg);
            g.FillEllipse(brush, 0, 0, 15, 15);
            using var font = new Font("Segoe UI", 7, FontStyle.Bold, GraphicsUnit.Point);
            using var textBrush = new SolidBrush(fg);
            var size = g.MeasureString(letter, font);
            g.DrawString(letter, font, textBrush, (16 - size.Width) / 2f, (16 - size.Height) / 2f - 0.5f);
        }

        var hIcon = bmp.GetHicon();
        return (Icon)Icon.FromHandle(hIcon).Clone();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        foreach (var icon in _icons.Values)
            icon.Dispose();
        _icons.Clear();
    }
}
