using StayAwake.Helpers;
using StayAwake.Models;

namespace StayAwake.Services;

public sealed class TimerRuleService
{
    public RuleResult EvaluateTimed(AppSettings settings, DateTime utcNow)
    {
        if (settings.Mode != AwakeMode.Timed)
            return RuleResult.NotMatched("Timed mode inactive");

        if (!TimerHelpers.IsTimedActive(settings.TimedEndUtc, utcNow))
            return RuleResult.NotMatched("Timed expired", settings.TimedEndUtc?.ToString("o"));

        var remaining = TimerHelpers.Remaining(settings.TimedEndUtc, utcNow);
        return RuleResult.Matched("Timed active", remaining?.ToString(@"hh\:mm\:ss"));
    }

    public RuleResult EvaluateUntil(AppSettings settings, DateTime utcNow)
    {
        if (settings.Mode != AwakeMode.Until)
            return RuleResult.NotMatched("Until mode inactive");

        if (!TimerHelpers.IsTimedActive(settings.UntilEndUtc, utcNow))
            return RuleResult.NotMatched("Until expired", settings.UntilEndUtc?.ToString("o"));

        var remaining = TimerHelpers.Remaining(settings.UntilEndUtc, utcNow);
        return RuleResult.Matched("Until active", remaining?.ToString(@"hh\:mm\:ss"));
    }

    public static void StartTimed(AppSettings settings, DateTime utcNow)
    {
        settings.TimedEndUtc = TimerHelpers.CalculateTimedEndUtc(utcNow, settings.TimedDurationMinutes);
        settings.UntilEndUtc = null;
    }

    public static void StartUntil(AppSettings settings, DateTime utcNow, DateTime localNow)
    {
        settings.UntilEndUtc = TimerHelpers.CalculateUntilEndUtc(utcNow, localNow, settings.UntilTime);
        settings.TimedEndUtc = null;
    }
}
