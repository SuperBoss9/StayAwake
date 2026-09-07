using StayAwake.Helpers;
using StayAwake.Models;
using StayAwake.Services;

namespace StayAwake.Tests;

public class CommandLineAndHotkeyTests
{
    [Fact]
    public void Parse_TimedAndDisplay()
    {
        var opt = CommandLineService.Parse(new[] { "--timed", "45", "--display" });
        Assert.Equal(AwakeMode.Timed, opt.SetMode);
        Assert.Equal(45, opt.TimedMinutes);
        Assert.True(opt.KeepDisplay);
    }

    [Fact]
    public void Parse_TimerAlias_AndDisplayOnOff()
    {
        var opt = CommandLineService.Parse(new[] { "--timer", "120", "--display-on" });
        Assert.Equal(AwakeMode.Timed, opt.SetMode);
        Assert.Equal(120, opt.TimedMinutes);
        Assert.True(opt.KeepDisplay);

        var off = CommandLineService.Parse(new[] { "--display-off" });
        Assert.False(off.KeepDisplay);
    }

    [Fact]
    public void Parse_ProcessPidRunArgsStatus()
    {
        var opt = CommandLineService.Parse(new[]
        {
            "--process", "ffmpeg", "--pid", "4242",
            "--run", @"C:\tools\job.exe", "--args", "--fast",
            "--status"
        });
        Assert.Equal("ffmpeg", opt.ProcessName);
        Assert.Equal(4242, opt.ProcessId);
        Assert.Equal(@"C:\tools\job.exe", opt.LaunchPath);
        Assert.Equal("--fast", opt.LaunchArgs);
        Assert.True(opt.Status);
    }

    [Fact]
    public void Parse_Until()
    {
        var opt = CommandLineService.Parse(new[] { "--until", "23:30" });
        Assert.Equal(AwakeMode.Until, opt.SetMode);
        Assert.Equal("23:30", opt.UntilTime);
    }

    [Fact]
    public void SplitArgs_HandlesQuotes()
    {
        var parts = CommandLineService.SplitArgs("--run \"C:\\Program Files\\a.exe\" --args \"-x y\"");
        Assert.Equal(new[] { "--run", @"C:\Program Files\a.exe", "--args", "-x y" }, parts);
    }

    [Fact]
    public void HotkeyParser_Valid()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Alt+A", out var mods, out var vk, out _));
        Assert.True((mods & StayAwake.Interop.HotkeyInterop.MOD_CONTROL) != 0);
        Assert.True((mods & StayAwake.Interop.HotkeyInterop.MOD_ALT) != 0);
        Assert.Equal((uint)'A', vk);
    }

    [Fact]
    public void HotkeyParser_Invalid()
    {
        Assert.False(HotkeyParser.TryParse("A", out _, out _, out var err));
        Assert.False(string.IsNullOrEmpty(err));
    }
}
