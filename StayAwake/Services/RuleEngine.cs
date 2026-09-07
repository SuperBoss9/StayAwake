using StayAwake.Helpers;
using StayAwake.Models;

namespace StayAwake.Services;

public sealed class RuleEngine
{
    private readonly ProcessMonitorService _processMonitor;
    private readonly CpuMonitorService _cpuMonitor;
    private readonly NetworkMonitorService _networkMonitor;
    private readonly ScheduleMonitorService _scheduleMonitor;
    private readonly TimerRuleService _timerRules;

    public RuleEngine(
        ProcessMonitorService processMonitor,
        CpuMonitorService cpuMonitor,
        NetworkMonitorService networkMonitor,
        ScheduleMonitorService scheduleMonitor,
        TimerRuleService timerRules)
    {
        _processMonitor = processMonitor;
        _cpuMonitor = cpuMonitor;
        _networkMonitor = networkMonitor;
        _scheduleMonitor = scheduleMonitor;
        _timerRules = timerRules;
    }

    public (bool ShouldStayAwake, IReadOnlyList<RuleResult> Results, string Reasons) Evaluate(
        AppSettings settings,
        DateTime utcNow,
        DateTime localNow)
    {
        if (settings.IsPaused || settings.Mode == AwakeMode.Off)
            return (false, Array.Empty<RuleResult>(), settings.IsPaused ? "Paused" : "Off");

        switch (settings.Mode)
        {
            case AwakeMode.Indefinite:
                return (true, new[] { RuleResult.Matched("Indefinite") }, "Indefinite");

            case AwakeMode.Timed:
            {
                var r = _timerRules.EvaluateTimed(settings, utcNow);
                return (r.IsMatched, new[] { r }, r.IsMatched ? "Timed" : "Timed expired");
            }

            case AwakeMode.Until:
            {
                var r = _timerRules.EvaluateUntil(settings, utcNow);
                return (r.IsMatched, new[] { r }, r.IsMatched ? "Until" : "Until expired");
            }

            case AwakeMode.Rules:
                return EvaluateCombined(settings, utcNow, localNow);

            default:
                return (false, Array.Empty<RuleResult>(), "Off");
        }
    }

    private (bool ShouldStayAwake, IReadOnlyList<RuleResult> Results, string Reasons) EvaluateCombined(
        AppSettings settings,
        DateTime utcNow,
        DateTime localNow)
    {
        var results = new List<RuleResult>();
        var matches = new List<bool>();

        if (settings.ProcessRuleEnabled)
        {
            var r = _processMonitor.Evaluate(settings.ProcessRules);
            results.Add(r);
            matches.Add(r.IsMatched);
        }

        if (settings.CpuRuleEnabled)
        {
            var r = _cpuMonitor.Evaluate(settings.CpuThresholdPercent, settings.CpuGraceSeconds, utcNow);
            results.Add(r);
            matches.Add(r.IsMatched);
        }

        if (settings.NetworkRuleEnabled)
        {
            var r = _networkMonitor.Evaluate(settings.NetworkThresholdBytesPerSec, settings.NetworkGraceSeconds, utcNow);
            results.Add(r);
            matches.Add(r.IsMatched);
        }

        if (settings.ScheduleRuleEnabled)
        {
            var r = _scheduleMonitor.Evaluate(settings.ScheduleRules, localNow);
            results.Add(r);
            matches.Add(r.IsMatched);
        }

        if (matches.Count == 0)
            return (false, results, "No rules enabled");

        bool should = RuleLogicEvaluator.Evaluate(settings.RuleLogic, matches, out _);
        var reasonParts = results.Where(r => r.IsMatched).Select(r => r.Description).ToList();
        string reasons = should
            ? string.Join("; ", reasonParts)
            : $"Rules not met ({settings.RuleLogic})";

        return (should, results, reasons);
    }
}
