using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StayAwake.Models;
using StayAwake.Services;

namespace StayAwake.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AppController _controller;
    private readonly LocalizationService _loc;
    private readonly EventHistoryService _history;

    [ObservableProperty] private string _statusText = "Off";
    [ObservableProperty] private string _reasonsText = "";
    [ObservableProperty] private string _endsAtText = "";
    [ObservableProperty] private string _remainingText = "";
    [ObservableProperty] private string _batteryText = "";
    [ObservableProperty] private AwakeMode _selectedMode = AwakeMode.Off;
    [ObservableProperty] private bool _keepDisplayOn;
    [ObservableProperty] private bool _isPaused;
    [ObservableProperty] private bool _isAwakeActive;
    [ObservableProperty] private int _timedMinutes = 60;
    [ObservableProperty] private int _selectedPresetMinutes = 60;
    [ObservableProperty] private string _untilTime = "22:00";
    [ObservableProperty] private RuleLogic _ruleLogic = RuleLogic.Any;
    [ObservableProperty] private bool _processRuleEnabled;
    [ObservableProperty] private bool _cpuRuleEnabled;
    [ObservableProperty] private bool _networkRuleEnabled;
    [ObservableProperty] private bool _scheduleRuleEnabled;
    [ObservableProperty] private double _cpuThreshold = 25;
    [ObservableProperty] private long _networkThresholdKb = 50;
    [ObservableProperty] private string? _selectedProfileName;
    private bool _suppressSettingsPush;

    public ObservableCollection<EventLogEntry> Events { get; } = new();
    public ObservableCollection<RuleResult> RuleResults { get; } = new();
    public ObservableCollection<string> ProfileNames { get; } = new();

    public int[] TimerPresets { get; } = { 15, 30, 60, 120, 240, 480 };
    public Array Modes { get; } = Enum.GetValues(typeof(AwakeMode));
    public Array RuleLogics { get; } = Enum.GetValues(typeof(RuleLogic));

    public event Action? FocusUntilRequested;
    public event Action? ProcessPickRequested;

    public MainViewModel(AppController controller, LocalizationService loc, EventHistoryService history)
    {
        _controller = controller;
        _loc = loc;
        _history = history;
        _controller.StateChanged += OnStateChanged;
        _controller.SettingsChanged += LoadFromSettings;
        _history.Changed += RefreshEvents;
        LoadFromSettings();
        RefreshEvents();
    }

    private void LoadFromSettings()
    {
        var s = _controller.Settings;
        _suppressSettingsPush = true;
        SelectedMode = s.Mode;
        KeepDisplayOn = s.KeepDisplayOn;
        IsPaused = s.IsPaused;
        TimedMinutes = s.TimedDurationMinutes;
        SelectedPresetMinutes = TimerPresets.Contains(s.TimedDurationMinutes)
            ? s.TimedDurationMinutes
            : 60;
        UntilTime = s.UntilTime;
        RuleLogic = s.RuleLogic;
        ProcessRuleEnabled = s.ProcessRuleEnabled;
        CpuRuleEnabled = s.CpuRuleEnabled;
        NetworkRuleEnabled = s.NetworkRuleEnabled;
        ScheduleRuleEnabled = s.ScheduleRuleEnabled;
        CpuThreshold = s.CpuThresholdPercent;
        NetworkThresholdKb = Math.Max(1, s.NetworkThresholdBytesPerSec / 1000);
        ProfileNames.Clear();
        foreach (var p in s.Profiles)
            ProfileNames.Add(p.Name);
        SelectedProfileName = s.ActiveProfileName;
        _suppressSettingsPush = false;
    }

    private void OnStateChanged(AwakeStateSnapshot snap)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusText = snap.StatusText;
            ReasonsText = snap.ReasonsText;
            IsAwakeActive = snap.IsAwakeActive;
            IsPaused = snap.IsPaused;
            KeepDisplayOn = snap.KeepDisplayOn;
            EndsAtText = snap.EndsAtLocal.HasValue
                ? snap.EndsAtLocal.Value.ToString("g")
                : "";
            RemainingText = snap.Remaining.HasValue && snap.Remaining.Value > TimeSpan.Zero
                ? snap.FormatRemaining()
                : "";
            BatteryText = snap.OnBattery
                ? $"{snap.BatteryPercent}%"
                : _loc.Get("BatteryAc", "AC");
            RuleResults.Clear();
            foreach (var r in snap.RuleResults)
                RuleResults.Add(r);
            if (SelectedMode != snap.Mode)
            {
                _suppressSettingsPush = true;
                SelectedMode = snap.Mode;
                _suppressSettingsPush = false;
            }
        });
    }

    private void RefreshEvents()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Events.Clear();
            foreach (var e in _history.GetEntries())
                Events.Add(e);
        });
    }

    partial void OnSelectedModeChanged(AwakeMode value)
    {
        if (_suppressSettingsPush) return;
        int? mins = value == AwakeMode.Timed ? TimedMinutes : null;
        string? until = value == AwakeMode.Until ? UntilTime : null;
        _controller.SetMode(value, mins, until);
    }

    partial void OnKeepDisplayOnChanged(bool value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.KeepDisplayOn = value);
    }

    partial void OnTimedMinutesChanged(int value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.TimedDurationMinutes = Math.Max(1, value), evaluate: false);
    }

    partial void OnSelectedPresetMinutesChanged(int value)
    {
        if (_suppressSettingsPush) return;
        TimedMinutes = value;
    }

    partial void OnUntilTimeChanged(string value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.UntilTime = value ?? "22:00", evaluate: false);
    }

    partial void OnRuleLogicChanged(RuleLogic value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.RuleLogic = value);
    }

    partial void OnProcessRuleEnabledChanged(bool value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.ProcessRuleEnabled = value);
    }

    partial void OnCpuRuleEnabledChanged(bool value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.CpuRuleEnabled = value);
    }

    partial void OnNetworkRuleEnabledChanged(bool value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.NetworkRuleEnabled = value);
    }

    partial void OnScheduleRuleEnabledChanged(bool value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.ScheduleRuleEnabled = value);
    }

    partial void OnCpuThresholdChanged(double value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.CpuThresholdPercent = value);
    }

    partial void OnNetworkThresholdKbChanged(long value)
    {
        if (_suppressSettingsPush) return;
        _controller.UpdateSettings(s => s.NetworkThresholdBytesPerSec = Math.Max(1, value) * 1000);
    }

    [RelayCommand]
    private void TogglePause() => _controller.TogglePause();

    [RelayCommand]
    private void ToggleAwake() => _controller.ToggleAwake();

    [RelayCommand]
    private void ApplyTimed()
    {
        _controller.SetMode(AwakeMode.Timed, TimedMinutes, null);
        _suppressSettingsPush = true;
        SelectedMode = AwakeMode.Timed;
        _suppressSettingsPush = false;
    }

    [RelayCommand]
    private void ApplyUntil()
    {
        _controller.SetMode(AwakeMode.Until, null, UntilTime);
        _suppressSettingsPush = true;
        SelectedMode = AwakeMode.Until;
        _suppressSettingsPush = false;
    }

    [RelayCommand]
    private void ApplyProfile()
    {
        if (!string.IsNullOrWhiteSpace(SelectedProfileName))
            _controller.ApplyProfile(SelectedProfileName!);
    }

    [RelayCommand]
    private void PickProcess() => ProcessPickRequested?.Invoke();

    [RelayCommand]
    private void FocusUntil() => FocusUntilRequested?.Invoke();

    public void AddProcessRule(ProcessRuleModel rule)
    {
        _controller.UpdateSettings(s =>
        {
            s.ProcessRuleEnabled = true;
            s.Mode = AwakeMode.Rules;
            s.IsPaused = false;
            s.ProcessRules.RemoveAll(r =>
                string.Equals(r.ProcessName, rule.ProcessName, StringComparison.OrdinalIgnoreCase)
                && r.ProcessId == rule.ProcessId);
            s.ProcessRules.Add(rule);
        });
        _suppressSettingsPush = true;
        SelectedMode = AwakeMode.Rules;
        ProcessRuleEnabled = true;
        _suppressSettingsPush = false;
    }

    public void Detach()
    {
        _controller.StateChanged -= OnStateChanged;
        _controller.SettingsChanged -= LoadFromSettings;
        _history.Changed -= RefreshEvents;
    }
}
