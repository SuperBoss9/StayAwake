using StayAwake.Models;

namespace StayAwake.Helpers;

public static class RuleLogicEvaluator
{
    public static bool Evaluate(RuleLogic logic, IReadOnlyList<bool> ruleMatches, out IReadOnlyList<int> matchedIndexes)
    {
        var matched = new List<int>();
        for (int i = 0; i < ruleMatches.Count; i++)
        {
            if (ruleMatches[i])
                matched.Add(i);
        }

        matchedIndexes = matched;

        if (ruleMatches.Count == 0)
            return false;

        return logic switch
        {
            RuleLogic.All => ruleMatches.All(x => x),
            _ => ruleMatches.Any(x => x)
        };
    }

    public static bool EvaluateResults(RuleLogic logic, IReadOnlyList<RuleResult> results)
    {
        if (results.Count == 0)
            return false;
        return logic == RuleLogic.All
            ? results.All(r => r.IsMatched)
            : results.Any(r => r.IsMatched);
    }
}
