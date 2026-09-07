using StayAwake.Helpers;
using StayAwake.Models;
using System.IO;
using System.Text;

namespace StayAwake.Services;

public sealed class AppController : IDisposable
{
    private readonly LoggingService _log;
    private readonly SettingsService _settingsService;
    private readonly PowerManager _power;
    private readonly RuleEngine _engine;
    private readonly BatteryService _battery;
    private readonly NotificationService _notifications;
    private readonly EventHistoryService _history;
    private readonly ProcessLauncherService _launcher;
    private readonly TimerRuleService _timerRules;
    private readonly object _sync = new();

    private AppSettings _settings;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _disposed;
    private bool _lastAwake;
    private bool _lastBatteryBlocked;
    private string _lastReasons = string.Empty;
    private bool _timerWarningSent;
    private DateTime? _warningForEndUtc;

    public AppSettings Settings
    {
        get { lock (_sync) return _settings; }
    }

    public AwakeStateSnapshot LastSnapshot { get; private set; } = new();

    public event Action<AwakeStateSnapshot>? StateChanged;
    public event Action? SettingsChanged;

    public AppController(
        LoggingService log,
        SettingsService settingsService,
        PowerManager power,
        RuleEngine engine,
        BatteryService battery,
        NotificationService notifications,
        EventHistoryService history,
        ProcessLauncherService launcher,
        TimerRuleService timerRules)
    {
        _log = log;
        _settingsService = settingsService;
        _power = power;
        _engine = engine;
        _battery = battery;
        _notifications = notifications;
        _history = history;
        _launcher = launcher;
        _timerRules = timerRules;
        _settings = _settingsService.Load();
        _launcher.ProcessExited += OnLaunchedProcessExited;
    }

    public void Start()
    {
        _loopCts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_loopCts.Token));
        _history.Add("App", "Controller started");
        _log.Info("AppController started");
        EvaluateAndApply(forceNotify: false);
    }

    public string ApplyCli(CliOptions opt)
    {
        if (opt.Status)
            return FormatStatusText();

        lock (_sync)
        {
            if (opt.Quit)
                return "OK";

            if (opt.KeepDisplay.HasValue)
                _settings.KeepDisplayOn = opt.KeepDisplay.Value;

            if (opt.Pause)
                _settings.IsPaused = true;
            else if (opt.Resume)
                _settings.IsPaused = false;

            if (opt.Toggle)
            {
                if (_settings.Mode == AwakeMode.Off || _settings.IsPaused)
                {
                    _settings.IsPaused = false;
                    _settings.Mode = AwakeMode.Indefinite;
                }
                else
                {
                    _settings.Mode = AwakeMode.Off;
                }
            }

            if (!string.IsNullOrWhiteSpace(opt.ProcessName) || opt.ProcessId.HasValue)
            {
                _settings.IsPaused = false;
                _settings.Mode = AwakeMode.Rules;
                _settings.ProcessRuleEnabled = true;
                var name = opt.ProcessName?.Trim() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name[..^4];

                _settings.ProcessRules.RemoveAll(r =>
                    (!string.IsNullOrEmpty(name) && string.Equals(r.ProcessName, name, StringComparison.OrdinalIgnoreCase))
                    || (opt.ProcessId.HasValue && r.ProcessId == opt.ProcessId));

                _settings.ProcessRules.Add(new ProcessRuleModel
                {
                    ProcessName = name,
                    ProcessId = opt.ProcessId,
                    Enabled = true,
                    DisplayName = opt.ProcessId.HasValue
                        ? $"{(string.IsNullOrEmpty(name) ? "PID" : name)} ({opt.ProcessId})"
                        : name
                });
            }

            if (opt.SetMode.HasValue)
            {
                _settings.IsPaused = false;
                SetModeCore(opt.SetMode.Value, opt.TimedMinutes, opt.UntilTime);
            }

            if (!string.IsNullOrWhiteSpace(opt.LaunchPath))
            {
                _settings.IsPaused = false;
                _settings.Mode = AwakeMode.Rules;
                _settings.ProcessRuleEnabled = true;
                _settings.LaunchArguments = opt.LaunchArgs;
                if (_launcher.Launch(opt.LaunchPath, _settings.LaunchWorkingDirectory, opt.LaunchArgs))
                {
                    _settings.LaunchCommand = opt.LaunchPath;
                    var name = _launcher.LaunchedName ?? Path.GetFileNameWithoutExtension(opt.LaunchPath);
                    _settings.ProcessRules.RemoveAll(r =>
                        string.Equals(r.ProcessName, name, StringComparison.OrdinalIgnoreCase));
                    _settings.ProcessRules.Add(new ProcessRuleModel
                    {
                        ProcessName = name!,
                        ProcessId = _launcher.LaunchedPid,
                        Enabled = true,
                        DisplayName = $"Launch: {name}"
                    });
                }
            }

            Persist();
        }

        EvaluateAndApply(forceNotify: true);
        SettingsChanged?.Invoke();
        return "OK";
    }

    public string FormatStatusText()
    {
        var snap = LastSnapshot;
        var sb = new StringBuilder();
        sb.AppendLine(snap.IsAwakeActive ? "Enabled" : (snap.IsPaused ? "Paused" : "Disabled"));
        sb.AppendLine($"Reason: {snap.ReasonsText}");
        if (snap.Remaining.HasValue && snap.Remaining.Value > TimeSpan.Zero)
            sb.AppendLine($"Remaining: {snap.Remaining.Value:hh\\:mm\\:ss}");
        else
            sb.AppendLine("Remaining: —");
        sb.AppendLine($"DisplayRequired: {snap.KeepDisplayOn && snap.IsAwakeActive}");
        sb.AppendLine($"Mode: {snap.Mode}");
        return sb.ToString().TrimEnd();
    }

    public void SetMode(AwakeMode mode, int? timedMinutes = null, string? untilTime = null)
    {
        lock (_sync)
        {
            _settings.IsPaused = false;
            SetModeCore(mode, timedMinutes, untilTime);
            Persist();
        }
        EvaluateAndApply(forceNotify: true);
        SettingsChanged?.Invoke();
    }

    private void SetModeCore(AwakeMode mode, int? timedMinutes, string? untilTime)
    {
        _settings.Mode = mode;
        if (timedMinutes.HasValue)
            _settings.TimedDurationMinutes = Math.Max(1, timedMinutes.Value);
        if (!string.IsNullOrWhiteSpace(untilTime))
            _settings.UntilTime = untilTime!;

        switch (mode)
        {
            case AwakeMode.Timed:
                TimerRuleService.StartTimed(_settings, DateTime.UtcNow);
                ResetTimerWarning();
                break;
            case AwakeMode.Until:
                TimerRuleService.StartUntil(_settings, DateTime.UtcNow, DateTime.Now);
                ResetTimerWarning();
                break;
            case AwakeMode.Off:
                _settings.TimedEndUtc = null;
                _settings.UntilEndUtc = null;
                ResetTimerWarning();
                break;
            default:
                ResetTimerWarning();
                break;
        }

        _history.Add("Mode", $"Mode → {mode}");
    }

    private void ResetTimerWarning()
    {
        _timerWarningSent = false;
        _warningForEndUtc = null;
    }

    public void SetPaused(bool paused)
    {
        lock (_sync)
        {
            _settings.IsPaused = paused;
            Persist();
        }
        _history.Add("Pause", paused ? "Paused" : "Resumed");
        EvaluateAndApply(forceNotify: true);
        SettingsChanged?.Invoke();
    }

    public void TogglePause() => SetPaused(!Settings.IsPaused);

    public void ToggleAwake()
    {
        lock (_sync)
        {
            if (_settings.Mode == AwakeMode.Off || _settings.IsPaused)
            {
                _settings.IsPaused = false;
                _settings.Mode = AwakeMode.Indefinite;
                _history.Add("Toggle", "On (Indefinite)");
            }
            else
            {
                _settings.Mode = AwakeMode.Off;
                _history.Add("Toggle", "Off");
            }
            Persist();
        }
        EvaluateAndApply(forceNotify: true);
        SettingsChanged?.Invoke();
    }

    public void ToggleDisplay()
    {
        lock (_sync)
        {
            _settings.KeepDisplayOn = !_settings.KeepDisplayOn;
            Persist();
        }
        _history.Add("Display", Settings.KeepDisplayOn ? "Display on" : "Display off");
        EvaluateAndApply(forceNotify: true);
        SettingsChanged?.Invoke();
    }

    public void ApplyProfile(string name)
    {
        lock (_sync)
        {
            var profile = _settings.Profiles.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
                return;

            _settings.ActiveProfileName = profile.Name;
            _settings.IsPaused = false;
            _settings.KeepDisplayOn = profile.KeepDisplayOn;
            _settings.RuleLogic = profile.RuleLogic;
            _settings.TimedDurationMinutes = profile.TimedDurationMinutes;
            _settings.UntilTime = profile.UntilTime;
            _settings.ProcessRuleEnabled = profile.ProcessRuleEnabled;
            _settings.CpuRuleEnabled = profile.CpuRuleEnabled;
            _settings.NetworkRuleEnabled = profile.NetworkRuleEnabled;
            _settings.ScheduleRuleEnabled = profile.ScheduleRuleEnabled;
            SetModeCore(profile.Mode,
                profile.Mode == AwakeMode.Timed ? profile.TimedDurationMinutes : null,
                profile.Mode == AwakeMode.Until ? profile.UntilTime : null);
            Persist();
            _history.Add("Profile", $"Applied {profile.Name}");
        }
        EvaluateAndApply(forceNotify: true);
        SettingsChanged?.Invoke();
    }

    public void UpdateSettings(Action<AppSettings> mutator, bool evaluate = true, bool notify = false)
    {
        lock (_sync)
        {
            mutator(_settings);
            Persist();
        }
        SettingsChanged?.Invoke();
        if (evaluate)
            EvaluateAndApply(forceNotify: notify);
    }

    public void ReplaceSettings(AppSettings settings, bool evaluate = true)
    {
        lock (_sync)
        {
            SettingsService.SanitizeAfterLoad(settings);
            SettingsService.EnsureDefaultProfiles(settings);
            _settings = settings;
            Persist();
        }
        SettingsChanged?.Invoke();
        if (evaluate)
            EvaluateAndApply(forceNotify: true);
    }

    private void Persist() => _settingsService.Save(_settings);

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                EvaluateAndApply(forceNotify: false);
                int delaySec;
                lock (_sync)
                {
                    delaySec = _settings.Mode == AwakeMode.Rules
                        ? Math.Min(
                            Math.Min(_settings.CpuPollSeconds, _settings.NetworkPollSeconds),
                            Math.Min(_settings.ProcessPollSeconds, _settings.SchedulePollSeconds))
                        : 2;
                }
                delaySec = Math.Clamp(delaySec, 1, 30);
                await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error("Controller loop error", ex);
                try { await Task.Delay(2000, ct); } catch { break; }
            }
        }
    }

    public void EvaluateAndApply(bool forceNotify)
    {
        AppSettings settings;
        lock (_sync) settings = CloneForEval(_settings);

        var utcNow = DateTime.UtcNow;
        var localNow = DateTime.Now;

        if (settings.Mode == AwakeMode.Timed && !TimerHelpers.IsTimedActive(settings.TimedEndUtc, utcNow))
        {
            lock (_sync)
            {
                if (_settings.Mode == AwakeMode.Timed)
                {
                    _settings.Mode = AwakeMode.Off;
                    _settings.TimedEndUtc = null;
                    Persist();
                    settings = CloneForEval(_settings);
                    _history.Add("Timer", "Timed expired → Off");
                    ResetTimerWarning();
                }
            }
        }
        else if (settings.Mode == AwakeMode.Until && !TimerHelpers.IsTimedActive(settings.UntilEndUtc, utcNow))
        {
            lock (_sync)
            {
                if (_settings.Mode == AwakeMode.Until)
                {
                    _settings.Mode = AwakeMode.Off;
                    _settings.UntilEndUtc = null;
                    Persist();
                    settings = CloneForEval(_settings);
                    _history.Add("Timer", "Until expired → Off");
                    ResetTimerWarning();
                }
            }
        }

        var (shouldAwake, results, reasons) = _engine.Evaluate(settings, utcNow, localNow);

        bool batteryBlocked = _battery.ShouldBlock(
            settings.AllowOnBattery,
            settings.DisableBelowBatteryPercent,
            settings.BatteryCutoffPercent);

        bool applyAwake = shouldAwake && !batteryBlocked && !settings.IsPaused;
        bool display = applyAwake && settings.KeepDisplayOn;

        if (applyAwake)
            _power.Apply(systemRequired: true, displayRequired: display);
        else
            _power.Release();

        DateTime? endsAt = null;
        TimeSpan? remaining = null;
        DateTime? endUtc = null;
        if (settings.Mode == AwakeMode.Timed && settings.TimedEndUtc.HasValue)
            endUtc = settings.TimedEndUtc;
        else if (settings.Mode == AwakeMode.Until && settings.UntilEndUtc.HasValue)
            endUtc = settings.UntilEndUtc;

        if (endUtc.HasValue)
        {
            endsAt = endUtc.Value.ToLocalTime();
            remaining = TimerHelpers.Remaining(endUtc, utcNow);
        }

        MaybeSendTimerWarning(settings, remaining, endUtc);

        string status;
        if (settings.IsPaused) status = "Paused";
        else if (settings.Mode == AwakeMode.Off) status = "Off";
        else if (batteryBlocked) status = "Blocked by battery";
        else if (applyAwake) status = "Keeping awake";
        else status = "Waiting for rules";

        var snapshot = new AwakeStateSnapshot
        {
            IsAwakeActive = applyAwake,
            IsPaused = settings.IsPaused,
            KeepDisplayOn = settings.KeepDisplayOn,
            Mode = settings.Mode,
            StatusText = status,
            ReasonsText = batteryBlocked
                ? $"Battery {_battery.Percent}% (cutoff {settings.BatteryCutoffPercent}%)"
                : reasons,
            EndsAtLocal = endsAt,
            Remaining = remaining,
            BatteryBlocked = batteryBlocked,
            BatteryPercent = _battery.Percent,
            OnBattery = _battery.OnBattery,
            RuleResults = results
        };

        LastSnapshot = snapshot;
        StateChanged?.Invoke(snapshot);

        bool stateChanged = applyAwake != _lastAwake
                            || batteryBlocked != _lastBatteryBlocked
                            || !string.Equals(reasons, _lastReasons, StringComparison.Ordinal);

        if ((forceNotify || stateChanged) && settings.ShowNotifications && settings.NotifyOnStateChange)
        {
            if (forceNotify || applyAwake != _lastAwake || batteryBlocked != _lastBatteryBlocked)
            {
                _notifications.Show("StayAwake", $"{status}: {snapshot.ReasonsText}", settings.ShowNotifications);
            }
        }

        if (stateChanged)
            _log.Info($"State: awake={applyAwake} mode={settings.Mode} reasons={snapshot.ReasonsText}");

        _lastAwake = applyAwake;
        _lastBatteryBlocked = batteryBlocked;
        _lastReasons = reasons;
    }

    private void MaybeSendTimerWarning(AppSettings settings, TimeSpan? remaining, DateTime? endUtc)
    {
        if (!settings.TimerWarningEnabled || !remaining.HasValue || !endUtc.HasValue)
            return;
        if (settings.Mode is not (AwakeMode.Timed or AwakeMode.Until))
            return;

        if (_warningForEndUtc != endUtc)
        {
            _timerWarningSent = false;
            _warningForEndUtc = endUtc;
        }

        var threshold = TimeSpan.FromMinutes(Math.Max(1, settings.TimerWarningMinutes));
        if (_timerWarningSent)
            return;
        if (remaining.Value <= threshold && remaining.Value > TimeSpan.Zero)
        {
            _timerWarningSent = true;
            var mins = Math.Max(1, (int)Math.Ceiling(remaining.Value.TotalMinutes));
            _notifications.Show("StayAwake",
                $"Timer will end in {mins} minute(s)",
                settings.ShowNotifications);
            _history.Add("Timer", $"Warning: {mins}m remaining");
        }
    }

    private void OnLaunchedProcessExited()
    {
        _history.Add("Launch", "Launched process exited");
        EvaluateAndApply(forceNotify: true);
    }

    private static AppSettings CloneForEval(AppSettings s) => new()
    {
        Mode = s.Mode,
        KeepDisplayOn = s.KeepDisplayOn,
        IsPaused = s.IsPaused,
        AllowOnBattery = s.AllowOnBattery,
        BatteryCutoffPercent = s.BatteryCutoffPercent,
        DisableBelowBatteryPercent = s.DisableBelowBatteryPercent,
        TimedDurationMinutes = s.TimedDurationMinutes,
        TimedEndUtc = s.TimedEndUtc,
        UntilTime = s.UntilTime,
        UntilEndUtc = s.UntilEndUtc,
        RuleLogic = s.RuleLogic,
        ProcessRuleEnabled = s.ProcessRuleEnabled,
        ProcessRules = s.ProcessRules,
        CpuRuleEnabled = s.CpuRuleEnabled,
        CpuThresholdPercent = s.CpuThresholdPercent,
        CpuGraceSeconds = s.CpuGraceSeconds,
        CpuPollSeconds = s.CpuPollSeconds,
        NetworkRuleEnabled = s.NetworkRuleEnabled,
        NetworkThresholdBytesPerSec = s.NetworkThresholdBytesPerSec,
        NetworkGraceSeconds = s.NetworkGraceSeconds,
        NetworkPollSeconds = s.NetworkPollSeconds,
        ScheduleRuleEnabled = s.ScheduleRuleEnabled,
        ScheduleRules = s.ScheduleRules,
        SchedulePollSeconds = s.SchedulePollSeconds,
        ProcessPollSeconds = s.ProcessPollSeconds,
        ShowNotifications = s.ShowNotifications,
        NotifyOnStateChange = s.NotifyOnStateChange,
        TimerWarningEnabled = s.TimerWarningEnabled,
        TimerWarningMinutes = s.TimerWarningMinutes
    };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _loopCts?.Cancel(); } catch { /* ignore */ }
        try { _loopCts?.Dispose(); } catch { /* ignore */ }
        _launcher.ProcessExited -= OnLaunchedProcessExited;
        _launcher.StopWatch();
        _power.Release();
    }
}
