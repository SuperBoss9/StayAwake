using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StayAwake.Models;
using StayAwake.Services;

namespace StayAwake.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppController _controller;
    private readonly StartupService _startup;
    private readonly ThemeService _theme;
    private readonly LocalizationService _loc;
    private readonly HotkeyService _hotkeys;
    private readonly SettingsService _settingsService;

    [ObservableProperty] private bool _allowOnBattery = true;
    [ObservableProperty] private bool _disableBelowBattery = true;
    [ObservableProperty] private int _batteryCutoff = 15;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized = true;
    [ObservableProperty] private bool _closeToTray = true;
    [ObservableProperty] private bool _showNotifications = true;
    [ObservableProperty] private bool _hotkeysEnabled = true;
    [ObservableProperty] private string _toggleHotkey = "Ctrl+Alt+A";
    [ObservableProperty] private string _displayHotkey = "Ctrl+Alt+D";
    [ObservableProperty] private ThemeMode _themeMode = ThemeMode.System;
    [ObservableProperty] private AppLanguage _language = AppLanguage.System;
    [ObservableProperty] private int _cpuGraceSeconds = 30;
    [ObservableProperty] private int _networkGraceSeconds = 30;
    [ObservableProperty] private int _cpuPollSeconds = 3;
    [ObservableProperty] private int _networkPollSeconds = 3;
    [ObservableProperty] private string _hotkeyStatus = "";
    [ObservableProperty] private string _launchCommand = "";
    [ObservableProperty] private bool _timerWarningEnabled = true;
    [ObservableProperty] private int _timerWarningMinutes = 5;
    [ObservableProperty] private string? _selectedProfileName;

    public ObservableCollection<ProcessRuleModel> ProcessRules { get; } = new();
    public ObservableCollection<ScheduleRuleModel> ScheduleRules { get; } = new();
    public ObservableCollection<string> ProfileNames { get; } = new();
    public int[] WarningMinuteOptions { get; } = { 1, 5, 10 };

    public Array Themes { get; } = Enum.GetValues(typeof(ThemeMode));
    public Array Languages { get; } = Enum.GetValues(typeof(AppLanguage));

    public SettingsViewModel(
        AppController controller,
        StartupService startup,
        ThemeService theme,
        LocalizationService loc,
        HotkeyService hotkeys,
        SettingsService settingsService)
    {
        _controller = controller;
        _startup = startup;
        _theme = theme;
        _loc = loc;
        _hotkeys = hotkeys;
        _settingsService = settingsService;
        Load();
    }

    public void Load()
    {
        var s = _controller.Settings;
        AllowOnBattery = s.AllowOnBattery;
        DisableBelowBattery = s.DisableBelowBatteryPercent;
        BatteryCutoff = s.BatteryCutoffPercent;
        StartWithWindows = s.StartWithWindows;
        StartMinimized = s.StartMinimizedToTray;
        CloseToTray = s.CloseToTray;
        ShowNotifications = s.ShowNotifications;
        HotkeysEnabled = s.HotkeysEnabled;
        ToggleHotkey = s.ToggleHotkey;
        DisplayHotkey = s.DisplayHotkey;
        ThemeMode = s.Theme;
        Language = s.Language;
        CpuGraceSeconds = s.CpuGraceSeconds;
        NetworkGraceSeconds = s.NetworkGraceSeconds;
        CpuPollSeconds = s.CpuPollSeconds;
        NetworkPollSeconds = s.NetworkPollSeconds;
        LaunchCommand = s.LaunchCommand ?? "";
        TimerWarningEnabled = s.TimerWarningEnabled;
        TimerWarningMinutes = s.TimerWarningMinutes;
        ProcessRules.Clear();
        foreach (var p in s.ProcessRules)
            ProcessRules.Add(p);
        ScheduleRules.Clear();
        foreach (var sch in s.ScheduleRules)
            ScheduleRules.Add(sch);
        ProfileNames.Clear();
        foreach (var p in s.Profiles)
            ProfileNames.Add(p.Name);
        SelectedProfileName = s.ActiveProfileName;
        HotkeyStatus = _hotkeys.LastError ?? "";
    }

    [RelayCommand]
    private void Save()
    {
        _controller.UpdateSettings(s =>
        {
            s.AllowOnBattery = AllowOnBattery;
            s.DisableBelowBatteryPercent = DisableBelowBattery;
            s.BatteryCutoffPercent = BatteryCutoff;
            s.StartWithWindows = StartWithWindows;
            s.StartMinimizedToTray = StartMinimized;
            s.CloseToTray = CloseToTray;
            s.ShowNotifications = ShowNotifications;
            s.HotkeysEnabled = HotkeysEnabled;
            s.ToggleHotkey = ToggleHotkey;
            s.DisplayHotkey = DisplayHotkey;
            s.Theme = ThemeMode;
            s.Language = Language;
            s.CpuGraceSeconds = CpuGraceSeconds;
            s.NetworkGraceSeconds = NetworkGraceSeconds;
            s.CpuPollSeconds = CpuPollSeconds;
            s.NetworkPollSeconds = NetworkPollSeconds;
            s.LaunchCommand = string.IsNullOrWhiteSpace(LaunchCommand) ? null : LaunchCommand;
            s.ProcessRules = ProcessRules.ToList();
            s.ScheduleRules = ScheduleRules.ToList();
            s.TimerWarningEnabled = TimerWarningEnabled;
            s.TimerWarningMinutes = TimerWarningMinutes;
        }, evaluate: true, notify: false);

        _startup.SetEnabled(StartWithWindows);
        _theme.Apply(ThemeMode);
        _loc.Apply(Language);
        _hotkeys.RegisterAll(ToggleHotkey, DisplayHotkey, HotkeysEnabled);
        HotkeyStatus = _hotkeys.LastError ?? _loc.Get("HotkeysOk", "Hotkeys OK");
    }

    [RelayCommand]
    private void ApplyProfile()
    {
        if (!string.IsNullOrWhiteSpace(SelectedProfileName))
        {
            _controller.ApplyProfile(SelectedProfileName!);
            Load();
        }
    }

    [RelayCommand]
    private void ExportSettings()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON|*.json",
            FileName = "stayawake-settings.json"
        };
        if (dlg.ShowDialog() != true)
            return;
        try
        {
            _settingsService.ExportTo(dlg.FileName, _controller.Settings);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Export",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ImportSettings()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON|*.json|All|*.*"
        };
        if (dlg.ShowDialog() != true)
            return;
        try
        {
            var imported = _settingsService.ImportFrom(dlg.FileName);
            _controller.ReplaceSettings(imported);
            Load();
            _hotkeys.RegisterAll(imported.ToggleHotkey, imported.DisplayHotkey, imported.HotkeysEnabled);
            _theme.Apply(imported.Theme);
            _loc.Apply(imported.Language);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Import",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void AddProcessRule()
    {
        ProcessRules.Add(new ProcessRuleModel { ProcessName = "notepad", Enabled = true });
    }

    [RelayCommand]
    private void RemoveProcessRule(ProcessRuleModel? rule)
    {
        if (rule != null)
            ProcessRules.Remove(rule);
    }

    [RelayCommand]
    private void AddScheduleRule()
    {
        ScheduleRules.Add(new ScheduleRuleModel { Name = "New schedule", Enabled = true });
    }

    [RelayCommand]
    private void RemoveScheduleRule(ScheduleRuleModel? rule)
    {
        if (rule != null)
            ScheduleRules.Remove(rule);
    }

    [RelayCommand]
    private void BrowseLaunch()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executables|*.exe;*.bat;*.cmd;*.ps1|All|*.*"
        };
        if (dlg.ShowDialog() == true)
            LaunchCommand = dlg.FileName;
    }

    [RelayCommand]
    private void LaunchUnderAwake()
    {
        if (string.IsNullOrWhiteSpace(LaunchCommand))
            return;
        _controller.ApplyCli(new CliOptions { LaunchPath = LaunchCommand });
    }

    [RelayCommand]
    private void OpenLogsFolder()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StayAwake", "Logs");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "StayAwake");
        }
    }

    [RelayCommand]
    private void OpenSettingsFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsService.SettingsPath)!;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "StayAwake");
        }
    }

    [RelayCommand]
    private void ResetSettings()
    {
        var confirm = System.Windows.MessageBox.Show(
            _loc.Get("ResetSettingsConfirm", "Reset all settings to defaults?"),
            "StayAwake",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes)
            return;

        var fresh = SettingsService.CreateDefault();
        _controller.ReplaceSettings(fresh);
        _startup.SetEnabled(fresh.StartWithWindows);
        _theme.Apply(fresh.Theme);
        _loc.Apply(fresh.Language);
        _hotkeys.RegisterAll(fresh.ToggleHotkey, fresh.DisplayHotkey, fresh.HotkeysEnabled, fresh.PauseHotkey);
        Load();
    }
}
