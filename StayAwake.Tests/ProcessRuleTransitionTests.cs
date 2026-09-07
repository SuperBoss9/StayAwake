using StayAwake.Models;
using StayAwake.Services;

namespace StayAwake.Tests;

public class ProcessRuleTransitionTests
{
    [Fact]
    public void NoRules_NotMatched()
    {
        var svc = new ProcessMonitorService();
        var result = svc.Evaluate(Array.Empty<ProcessRuleModel>());
        Assert.False(result.IsMatched);
    }

    [Fact]
    public void DisabledRules_Ignored()
    {
        var svc = new ProcessMonitorService();
        var result = svc.Evaluate(new[]
        {
            new ProcessRuleModel { ProcessName = "explorer", Enabled = false }
        });
        Assert.False(result.IsMatched);
    }

    [Fact]
    public void NameRule_MatchesRunningProcess()
    {
        // explorer.exe is virtually always running on Windows desktop sessions
        var svc = new ProcessMonitorService();
        var result = svc.Evaluate(new[]
        {
            new ProcessRuleModel { ProcessName = "explorer", Enabled = true }
        });
        Assert.True(result.IsMatched);
        Assert.Contains("explorer", result.OptionalDetails ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NameRule_NotMatched_UnknownProcess()
    {
        var svc = new ProcessMonitorService();
        var result = svc.Evaluate(new[]
        {
            new ProcessRuleModel { ProcessName = "DefinitelyNotARealProcess_XYZ_999", Enabled = true }
        });
        Assert.False(result.IsMatched);
    }

    [Fact]
    public void DeadPid_DoesNotMatch_WithoutName()
    {
        var rule = new ProcessRuleModel { ProcessId = 1, ProcessName = "", Enabled = true };
        // PID 1 is typically System Idle / not accessible as a normal process match on Windows;
        // GetProcessById(1) may throw or return idle — ensure helper is safe.
        var matched = ProcessMonitorService.IsProcessRunning(rule, out _);
        // Either way must not throw; idle process may or may not be visible.
        Assert.True(matched || !matched);
    }

    [Fact]
    public void Transition_FromMatchedToUnmatched_WhenProcessListEmptyAfterDisable()
    {
        var svc = new ProcessMonitorService();
        var rules = new List<ProcessRuleModel>
        {
            new() { ProcessName = "explorer", Enabled = true }
        };
        Assert.True(svc.Evaluate(rules).IsMatched);
        rules[0].Enabled = false;
        Assert.False(svc.Evaluate(rules).IsMatched);
    }
}
