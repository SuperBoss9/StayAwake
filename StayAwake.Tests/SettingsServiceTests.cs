using StayAwake.Models;
using StayAwake.Services;

namespace StayAwake.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "StayAwakeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var log = new LoggingService(Path.Combine(dir, "logs"));
            var svc = new SettingsService(log, dir);
            var settings = SettingsService.CreateDefault();
            settings.Mode = AwakeMode.Indefinite;
            settings.KeepDisplayOn = true;
            settings.BatteryCutoffPercent = 25;
            settings.ToggleHotkey = "Ctrl+Alt+Z";
            settings.ProcessRules.Add(new ProcessRuleModel { ProcessName = "chrome", Enabled = true });
            svc.Save(settings);

            var loaded = svc.Load();
            Assert.Equal(AwakeMode.Indefinite, loaded.Mode);
            Assert.True(loaded.KeepDisplayOn);
            Assert.Equal(25, loaded.BatteryCutoffPercent);
            Assert.Equal("Ctrl+Alt+Z", loaded.ToggleHotkey);
            Assert.Single(loaded.ProcessRules);
            Assert.Equal("chrome", loaded.ProcessRules[0].ProcessName);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void CorruptFile_RecoversDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "StayAwakeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var log = new LoggingService(Path.Combine(dir, "logs"));
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{ not valid json !!!");
            var svc = new SettingsService(log, dir);
            var loaded = svc.Load();
            Assert.Equal(AwakeMode.Off, loaded.Mode);
            Assert.True(File.Exists(path));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Sanitize_DropsExpiredTimer()
    {
        var s = new AppSettings
        {
            Mode = AwakeMode.Timed,
            TimedEndUtc = DateTime.UtcNow.AddMinutes(-10),
            UntilEndUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        SettingsService.SanitizeAfterLoad(s);
        Assert.Null(s.TimedEndUtc);
        Assert.Null(s.UntilEndUtc);
        Assert.Equal(AwakeMode.Off, s.Mode);
    }

    [Fact]
    public void Sanitize_ExpiredUntil_SetsOff()
    {
        var s = new AppSettings
        {
            Mode = AwakeMode.Until,
            UntilEndUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        SettingsService.SanitizeAfterLoad(s);
        Assert.Null(s.UntilEndUtc);
        Assert.Equal(AwakeMode.Off, s.Mode);
    }

    [Fact]
    public void Sanitize_KeepsFutureTimer()
    {
        var future = DateTime.UtcNow.AddHours(2);
        var s = new AppSettings { Mode = AwakeMode.Timed, TimedEndUtc = future };
        SettingsService.SanitizeAfterLoad(s);
        Assert.Equal(future, s.TimedEndUtc);
        Assert.Equal(AwakeMode.Timed, s.Mode);
    }

    [Fact]
    public void CreateDefault_SeedsProfiles()
    {
        var s = SettingsService.CreateDefault();
        Assert.Contains(s.Profiles, p => p.Name == "Presentation" && p.Mode == AwakeMode.Indefinite && p.KeepDisplayOn);
        Assert.Contains(s.Profiles, p => p.Name == "Download" && p.NetworkRuleEnabled && !p.KeepDisplayOn);
        Assert.Contains(s.Profiles, p => p.Name == "Backup" && p.ProcessRuleEnabled);
        Assert.Contains(s.Profiles, p => p.Name == "Workday" && p.ScheduleRuleEnabled);
    }

    [Fact]
    public void CorruptBackup_UsesBrokenName()
    {
        var dir = Path.Combine(Path.GetTempPath(), "StayAwakeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var log = new LoggingService(Path.Combine(dir, "logs"));
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{ not valid json !!!");
            var svc = new SettingsService(log, dir);
            _ = svc.Load();
            var broken = Directory.GetFiles(dir, "settings.broken.*.json");
            Assert.NotEmpty(broken);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
