using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using StayAwake.Helpers;
using StayAwake.Models;

namespace StayAwake.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly LoggingService _log;
    private readonly string _settingsPath;
    private readonly object _sync = new();

    public SettingsService(LoggingService log, string? settingsDirectory = null)
    {
        _log = log;
        var dir = settingsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StayAwake");
        Directory.CreateDirectory(dir);
        _settingsPath = Path.Combine(dir, "settings.json");
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    var fresh = CreateDefault();
                    Save(fresh);
                    return fresh;
                }

                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings == null)
                    throw new InvalidOperationException("Deserialized null settings");

                SanitizeAfterLoad(settings);
                EnsureDefaultProfiles(settings);
                return settings;
            }
            catch (Exception ex)
            {
                _log.Error("Failed to load settings; recovering", ex);
                TryBackupCorrupt();
                var recovered = CreateDefault();
                Save(recovered);
                return recovered;
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_sync)
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath)!;
                Directory.CreateDirectory(dir);
                var tmp = _settingsPath + ".tmp";
                var json = Serialize(settings);
                File.WriteAllText(tmp, json);
                File.Copy(tmp, _settingsPath, overwrite: true);
                File.Delete(tmp);
            }
            catch (Exception ex)
            {
                _log.Error("Failed to save settings", ex);
            }
        }
    }

    public static string Serialize(AppSettings settings) =>
        JsonSerializer.Serialize(settings, JsonOptions);

    public static AppSettings? Deserialize(string json)
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        if (settings != null)
        {
            SanitizeAfterLoad(settings);
            EnsureDefaultProfiles(settings);
        }
        return settings;
    }

    public void ExportTo(string path, AppSettings settings)
    {
        File.WriteAllText(path, Serialize(settings));
    }

    public AppSettings ImportFrom(string path)
    {
        var json = File.ReadAllText(path);
        var settings = Deserialize(json)
                       ?? throw new InvalidOperationException("Invalid settings file");
        return settings;
    }

    public static AppSettings CreateDefault()
    {
        var settings = new AppSettings
        {
            ToggleHotkey = "Ctrl+Alt+A",
            DisplayHotkey = "Ctrl+Alt+D",
            ScheduleRules =
            {
                new ScheduleRuleModel
                {
                    Name = "Work hours",
                    Enabled = true,
                    StartTime = new TimeSpan(9, 0, 0),
                    EndTime = new TimeSpan(18, 0, 0),
                    DaysOfWeek = 0b0111110
                }
            }
        };
        EnsureDefaultProfiles(settings);
        return settings;
    }

    public static void EnsureDefaultProfiles(AppSettings settings)
    {
        settings.Profiles ??= new List<ProfileModel>();
        void Ensure(string name, Action<ProfileModel> configure)
        {
            if (settings.Profiles.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                return;
            var p = new ProfileModel { Name = name };
            configure(p);
            settings.Profiles.Add(p);
        }

        Ensure("Presentation", p =>
        {
            p.Mode = AwakeMode.Indefinite;
            p.KeepDisplayOn = true;
        });
        Ensure("Download", p =>
        {
            p.Mode = AwakeMode.Rules;
            p.NetworkRuleEnabled = true;
            p.KeepDisplayOn = false;
            p.RuleLogic = RuleLogic.Any;
        });
        Ensure("Backup", p =>
        {
            p.Mode = AwakeMode.Rules;
            p.ProcessRuleEnabled = true;
            p.KeepDisplayOn = false;
            p.RuleLogic = RuleLogic.Any;
        });
        Ensure("Workday", p =>
        {
            p.Mode = AwakeMode.Rules;
            p.ScheduleRuleEnabled = true;
            p.KeepDisplayOn = false;
            p.RuleLogic = RuleLogic.Any;
        });
    }

    public static void SanitizeAfterLoad(AppSettings settings)
    {
        var utcNow = DateTime.UtcNow;
        bool timedCleared = settings.TimedEndUtc.HasValue
                            && TimerHelpers.SanitizePersistedEnd(settings.TimedEndUtc, utcNow) == null;
        bool untilCleared = settings.UntilEndUtc.HasValue
                            && TimerHelpers.SanitizePersistedEnd(settings.UntilEndUtc, utcNow) == null;

        settings.TimedEndUtc = TimerHelpers.SanitizePersistedEnd(settings.TimedEndUtc, utcNow);
        settings.UntilEndUtc = TimerHelpers.SanitizePersistedEnd(settings.UntilEndUtc, utcNow);

        if (settings.Mode == AwakeMode.Timed && (timedCleared || !settings.TimedEndUtc.HasValue))
            settings.Mode = AwakeMode.Off;
        if (settings.Mode == AwakeMode.Until && (untilCleared || !settings.UntilEndUtc.HasValue))
            settings.Mode = AwakeMode.Off;

        // Migrate old PauseHotkey-only configs: if DisplayHotkey missing/default-empty use Ctrl+Alt+D
        if (string.IsNullOrWhiteSpace(settings.DisplayHotkey))
            settings.DisplayHotkey = "Ctrl+Alt+D";
        if (string.IsNullOrWhiteSpace(settings.ToggleHotkey))
            settings.ToggleHotkey = "Ctrl+Alt+A";

        // Drop PID rules that no longer exist; keep name-based rules.
        foreach (var rule in settings.ProcessRules.ToList())
        {
            if (rule.ProcessId is int pid)
            {
                try
                {
                    var p = System.Diagnostics.Process.GetProcessById(pid);
                    if (p.HasExited)
                        rule.ProcessId = null;
                }
                catch
                {
                    rule.ProcessId = null;
                }
            }
        }

        settings.BatteryCutoffPercent = BatteryPolicy.ClampPercent(settings.BatteryCutoffPercent);
        if (settings.TimedDurationMinutes < 1)
            settings.TimedDurationMinutes = 1;
        if (settings.TimerWarningMinutes < 1)
            settings.TimerWarningMinutes = 5;
        if (settings.CpuPollSeconds < 1) settings.CpuPollSeconds = 3;
        if (settings.NetworkPollSeconds < 1) settings.NetworkPollSeconds = 3;
        if (settings.ProcessPollSeconds < 1) settings.ProcessPollSeconds = 2;
        if (settings.SchedulePollSeconds < 1) settings.SchedulePollSeconds = 15;
    }

    private void TryBackupCorrupt()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return;
            var dir = Path.GetDirectoryName(_settingsPath)!;
            var bak = Path.Combine(dir, $"settings.broken.{DateTime.Now:yyyyMMddHHmmss}.json");
            File.Copy(_settingsPath, bak, overwrite: true);
        }
        catch
        {
            // ignore
        }
    }
}
