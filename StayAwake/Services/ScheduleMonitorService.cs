using StayAwake.Helpers;
using StayAwake.Models;

namespace StayAwake.Services;

public sealed class ScheduleMonitorService
{
    public RuleResult Evaluate(IEnumerable<ScheduleRuleModel> rules, DateTime localNow)
    {
        var list = rules.Where(r => r.Enabled).ToList();
        if (list.Count == 0)
            return RuleResult.NotMatched("No schedule rules");

        var active = list.Where(r => ScheduleHelpers.IsWithinSchedule(r, localNow)).ToList();
        if (active.Count > 0)
            return RuleResult.Matched("Schedule active", string.Join(", ", active.Select(a => a.Name)));

        return RuleResult.NotMatched("Outside schedule");
    }
}
