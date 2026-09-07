using System.Text.Json.Serialization;

namespace StayAwake.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    public AwakeMode Mode { get; set; } = AwakeMode.Off;
    public bool KeepDisplayOn { get; set; }
    public bool IsPaused { get; set; }

    public bool AllowOnBattery { get; set; } = true;
    public int BatteryCutoffPercent { get; set; } = 15;
    public bool DisableBelowBatteryPercent { get; set; } = true;

    public int TimedDurationMinutes { get; set; } = 60;
    public DateTime? TimedEndUtc { get; set; }

    public string UntilTime { get; set; } = "22:00";
    public DateTime? UntilEndUtc { get; set; }

    public RuleLogic RuleLogic { get; set; } = RuleLogic.Any;

    public bool ProcessRuleEnabled { get; set; }
    public List<ProcessRuleModel> ProcessRules { get; set; } = new();

    public bool CpuRuleEnabled { get; set; }
    public double CpuThresholdPercent { get; set; } = 25;
    public int CpuGraceSeconds { get; set; } = 30;
    public int CpuPollSeconds { get; set; } = 3;

    public bool NetworkRuleEnabled { get; set; }
    public long NetworkThresholdBytesPerSec { get; set; } = 50_000;
    public int NetworkGraceSeconds { get; set; } = 30;
    public int NetworkPollSeconds { get; set; } = 3;

    public bool ScheduleRuleEnabled { get; set; }
    public List<ScheduleRuleModel> ScheduleRules { get; set; } = new();
    public int SchedulePollSeconds { get; set; } = 15;

    public int ProcessPollSeconds { get; set; } = 2;

    public string? LaunchCommand { get; set; }
    public string? LaunchArguments { get; set; }
    public string? LaunchWorkingDirectory { get; set; }
    public bool LaunchWaitForExit { get; set; } = true;

    public string ToggleHotkey { get; set; } = "Ctrl+Alt+A";
    public string DisplayHotkey { get; set; } = "Ctrl+Alt+D";
    public string? PauseHotkey { get; set; }
    public bool HotkeysEnabled { get; set; } = true;

    public bool StartWithWindows { get; set; }
    public bool StartMinimizedToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;

    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public AppLanguage Language { get; set; } = AppLanguage.System;

    public bool ShowNotifications { get; set; } = true;
    public bool NotifyOnStateChange { get; set; } = true;

    public bool TimerWarningEnabled { get; set; } = true;
    public int TimerWarningMinutes { get; set; } = 5;

    public List<ProfileModel> Profiles { get; set; } = new();
    public string? ActiveProfileName { get; set; }

    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 420;
    public double WindowHeight { get; set; } = 560;

    [JsonIgnore]
    public bool HasActiveTimedEnd => TimedEndUtc.HasValue && TimedEndUtc.Value > DateTime.UtcNow;

    [JsonIgnore]
    public bool HasActiveUntilEnd => UntilEndUtc.HasValue && UntilEndUtc.Value > DateTime.UtcNow;
}
