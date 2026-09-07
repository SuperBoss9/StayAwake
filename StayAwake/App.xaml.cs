using System.IO;
using System.Windows;
using StayAwake.Models;
using StayAwake.Services;
using StayAwake.ViewModels;
using StayAwake.Views;

namespace StayAwake;

public partial class App : Application
{
    private LoggingService? _log;
    private SingleInstanceService? _singleInstance;
    private SettingsService? _settingsService;
    private AppController? _controller;
    private PowerManager? _power;
    private TrayService? _tray;
    private HotkeyService? _hotkeys;
    private ThemeService? _theme;
    private LocalizationService? _loc;
    private StartupService? _startup;
    private MainWindow? _mainWindow;
    private bool _exitRequested;
    private bool _updatingDisplayMenu;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var cli = CommandLineService.Parse(e.Args);
        if (cli.ShowHelp)
        {
            MessageBox.Show(CommandLineService.HelpText, "StayAwake", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _log = new LoggingService();
        _singleInstance = new SingleInstanceService(_log);

        if (!_singleInstance.TryAcquire())
        {
            string forward = e.Args.Length == 0 ? "--show" : CommandLineService.RebuildCommandLine(e.Args);
            var reply = await SingleInstanceService.SendCommandAsync(forward);
            DeliverSecondInstanceReply(forward, reply, cli.Status || forward.Contains("--status", StringComparison.OrdinalIgnoreCase));
            Shutdown();
            return;
        }

        _singleInstance.StartServer();
        _singleInstance.CommandHandler = HandleIpcCommand;

        _settingsService = new SettingsService(_log);
        _power = new PowerManager(_log);
        var processMonitor = new ProcessMonitorService();
        var cpuMonitor = new CpuMonitorService();
        var networkMonitor = new NetworkMonitorService();
        var scheduleMonitor = new ScheduleMonitorService();
        var timerRules = new TimerRuleService();
        var engine = new RuleEngine(processMonitor, cpuMonitor, networkMonitor, scheduleMonitor, timerRules);
        var battery = new BatteryService();
        _tray = new TrayService();
        var notifications = new NotificationService(_tray, _log);
        var history = new EventHistoryService();
        var launcher = new ProcessLauncherService(_log);
        _startup = new StartupService(_log);
        _theme = new ThemeService();
        _loc = new LocalizationService();
        _hotkeys = new HotkeyService(_log);

        _controller = new AppController(
            _log, _settingsService, _power, engine, battery,
            notifications, history, launcher, timerRules);

        var settings = _controller.Settings;
        _theme.Apply(settings.Theme);
        _loc.Apply(settings.Language);
        _startup.SetEnabled(settings.StartWithWindows);

        var mainVm = new MainViewModel(_controller, _loc, history);
        _mainWindow = new MainWindow(mainVm, _controller, CreateSettingsWindow);

        _hotkeys.Attach(_mainWindow);
        _hotkeys.RegisterAll(settings.ToggleHotkey, settings.DisplayHotkey, settings.HotkeysEnabled, settings.PauseHotkey);
        _hotkeys.TogglePressed += () => Dispatcher.Invoke(() => _controller.ToggleAwake());
        _hotkeys.DisplayPressed += () => Dispatcher.Invoke(() => _controller.ToggleDisplay());
        _hotkeys.PausePressed += () => Dispatcher.Invoke(() => _controller.TogglePause());

        WireTray();
        _controller.StateChanged += OnStateChanged;
        _controller.Start();

        if (cli.Quit)
        {
            RequestExit();
            return;
        }

        if (cli.Status)
        {
            DeliverStatusLocal(_controller.FormatStatusText());
        }

        if (HasAction(cli))
            _controller.ApplyCli(cli);

        bool startHidden = cli.Minimized || settings.StartMinimizedToTray;
        if (cli.ShowWindow)
            _mainWindow.Show();
        else if (startHidden)
            _mainWindow.Hide();
        else
            _mainWindow.Show();
    }

    private static bool HasAction(CliOptions cli) =>
        cli.Toggle || cli.Pause || cli.Resume || cli.SetMode.HasValue
        || cli.KeepDisplay.HasValue || !string.IsNullOrWhiteSpace(cli.LaunchPath)
        || !string.IsNullOrWhiteSpace(cli.ProcessName) || cli.ProcessId.HasValue;

    private SettingsWindow CreateSettingsWindow()
    {
        var vm = new SettingsViewModel(_controller!, _startup!, _theme!, _loc!, _hotkeys!, _settingsService!);
        return new SettingsWindow(vm);
    }

    private string HandleIpcCommand(string command)
    {
        string result = "OK";
        Dispatcher.Invoke(() =>
        {
            if (_exitRequested)
            {
                result = "EXITING";
                return;
            }

            var args = CommandLineService.SplitArgs(command);
            var cli = CommandLineService.Parse(args);

            if (cli.Quit)
            {
                RequestExit();
                result = "OK";
                return;
            }

            if (cli.Status)
            {
                result = _controller?.FormatStatusText() ?? "Disabled";
                return;
            }

            if (cli.ShowWindow || cli.ShowHelp || (!HasAction(cli) && args.Length == 0))
            {
                _mainWindow?.ShowFromTray();
                result = "OK";
                return;
            }

            if (cli.ShowWindow)
                _mainWindow?.ShowFromTray();

            if (HasAction(cli))
                result = _controller?.ApplyCli(cli) ?? "OK";
            else
                _mainWindow?.ShowFromTray();
        });
        return result;
    }

    private void WireTray()
    {
        _tray!.OpenRequested += (_, _) => Dispatcher.Invoke(() => _mainWindow?.ShowFromTray());
        _tray.SettingsRequested += (_, _) => Dispatcher.Invoke(() =>
        {
            _mainWindow?.ShowFromTray();
            var win = CreateSettingsWindow();
            win.Owner = _mainWindow;
            win.ShowDialog();
        });
        _tray.IndefiniteRequested += (_, _) => Dispatcher.Invoke(() => _controller?.SetMode(AwakeMode.Indefinite));
        _tray.TimedMinutesRequested += (_, mins) => Dispatcher.Invoke(() => _controller?.SetMode(AwakeMode.Timed, mins));
        _tray.UntilRequested += (_, _) => Dispatcher.Invoke(() => _mainWindow?.FocusUntilSection());
        _tray.ProcessPickerRequested += (_, _) => Dispatcher.Invoke(() => _mainWindow?.OpenProcessPicker());
        _tray.PauseRequested += (_, _) => Dispatcher.Invoke(() => _controller?.TogglePause());
        _tray.ExitRequested += (_, _) => Dispatcher.Invoke(RequestExit);
        _tray.KeepDisplayChanged += (_, enabled) => Dispatcher.Invoke(() =>
        {
            if (_updatingDisplayMenu) return;
            _controller?.UpdateSettings(s => s.KeepDisplayOn = enabled);
        });

        UpdateTrayMenuLabels();
        _loc!.LanguageChanged += () => Dispatcher.Invoke(UpdateTrayMenuLabels);
    }

    private void UpdateTrayMenuLabels()
    {
        if (_tray == null || _loc == null) return;
        _tray.UpdateLocalizedLabels(
            _loc.Get("Open", "Open"),
            _loc.Get("ModeIndefinite", "Indefinite"),
            _loc.Get("ModeUntil", "Until…"),
            _loc.Get("PickProcess", "Process…"),
            _loc.Get("KeepDisplay", "Keep display on"),
            _loc.Get("Pause", "Pause"),
            _loc.Get("Exit", "Exit"),
            _loc.Get("Settings", "Settings"));
    }

    private void OnStateChanged(AwakeStateSnapshot snap)
    {
        Dispatcher.Invoke(() =>
        {
            if (_tray == null) return;
            TrayIconState state;
            if (snap.IsPaused) state = TrayIconState.Pause;
            else if (snap.BatteryBlocked) state = TrayIconState.Battery;
            else if (snap.IsAwakeActive) state = TrayIconState.On;
            else state = TrayIconState.Off;

            var tip = snap.Remaining.HasValue && snap.IsAwakeActive
                ? $"StayAwake — {snap.FormatRemaining()} left"
                : $"StayAwake — {snap.StatusText}";
            _tray.SetState(state, tip);

            _updatingDisplayMenu = true;
            try { _tray.UpdateStatus(snap); }
            finally { _updatingDisplayMenu = false; }
        });
    }

    private static void DeliverSecondInstanceReply(string forward, string? reply, bool isStatus)
    {
        if (string.IsNullOrWhiteSpace(reply))
            reply = "Failed to contact StayAwake instance.";

        if (isStatus || !string.Equals(reply.Trim(), "OK", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var path = Path.Combine(Path.GetTempPath(), "StayAwake.status.txt");
                File.WriteAllText(path, reply);
            }
            catch { /* ignore */ }

            try
            {
                Console.WriteLine(reply);
            }
            catch { /* WinExe may have no console */ }

            if (isStatus || forward.Contains("--status", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    MessageBox.Show(reply, "StayAwake status", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { /* ignore */ }
            }
        }
    }

    private static void DeliverStatusLocal(string status)
    {
        try
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "StayAwake.status.txt"), status);
        }
        catch { /* ignore */ }
        try { Console.WriteLine(status); } catch { /* ignore */ }
    }

    private void RequestExit()
    {
        if (_exitRequested) return;
        _exitRequested = true;
        try
        {
            _controller?.Dispose();
            _hotkeys?.Dispose();
            _tray?.Dispose();
            _power?.Dispose();
            _singleInstance?.Dispose();
            _log?.Dispose();
            _mainWindow?.ForceClose();
        }
        finally
        {
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!_exitRequested)
        {
            _controller?.Dispose();
            _hotkeys?.Dispose();
            _tray?.Dispose();
            _power?.Dispose();
            _singleInstance?.Dispose();
            _log?.Dispose();
        }
        base.OnExit(e);
    }
}
