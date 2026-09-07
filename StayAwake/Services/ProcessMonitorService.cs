using System.Diagnostics;
using StayAwake.Models;

namespace StayAwake.Services;

public sealed class ProcessMonitorService
{
    public RuleResult Evaluate(IEnumerable<ProcessRuleModel> rules)
    {
        var enabled = rules.Where(r => r.Enabled).ToList();
        if (enabled.Count == 0)
            return RuleResult.NotMatched("No process rules");

        var running = new List<string>();
        foreach (var rule in enabled)
        {
            if (IsProcessRunning(rule, out var detail))
                running.Add(detail);
        }

        if (running.Count > 0)
            return RuleResult.Matched("Process running", string.Join(", ", running));

        return RuleResult.NotMatched("No matching processes");
    }

    public static bool IsProcessRunning(ProcessRuleModel rule, out string detail)
    {
        detail = string.Empty;

        if (rule.ProcessId is int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                if (!p.HasExited)
                {
                    detail = $"{p.ProcessName} (PID {pid})";
                    return true;
                }
            }
            catch
            {
                // PID gone
            }
        }

        if (!string.IsNullOrWhiteSpace(rule.ProcessName))
        {
            var name = rule.ProcessName.Trim();
            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            try
            {
                var matches = Process.GetProcessesByName(name);
                if (matches.Length > 0)
                {
                    detail = $"{name} x{matches.Length}";
                    return true;
                }
            }
            catch
            {
                // ignore access issues
            }
        }

        return false;
    }

    public static IReadOnlyList<(int Pid, string Name)> GetRunningProcesses()
    {
        try
        {
            return Process.GetProcesses()
                .Select(p =>
                {
                    try { return (Pid: p.Id, Name: p.ProcessName); }
                    catch { return (Pid: -1, Name: string.Empty); }
                })
                .Where(x => x.Pid > 0 && !string.IsNullOrEmpty(x.Name))
                .OrderBy(x => x.Name)
                .ToList();
        }
        catch
        {
            return Array.Empty<(int, string)>();
        }
    }
}
