using StayAwake.Helpers;
using StayAwake.Models;

namespace StayAwake.Tests;

public class RuleLogicEvaluatorTests
{
    [Fact]
    public void Any_TrueIfOneMatches()
    {
        Assert.True(RuleLogicEvaluator.Evaluate(RuleLogic.Any, new[] { false, true, false }, out var idx));
        Assert.Equal(new[] { 1 }, idx);
    }

    [Fact]
    public void Any_FalseIfNone()
    {
        Assert.False(RuleLogicEvaluator.Evaluate(RuleLogic.Any, new[] { false, false }, out _));
    }

    [Fact]
    public void All_RequiresEvery()
    {
        Assert.False(RuleLogicEvaluator.Evaluate(RuleLogic.All, new[] { true, false }, out _));
        Assert.True(RuleLogicEvaluator.Evaluate(RuleLogic.All, new[] { true, true }, out _));
    }

    [Fact]
    public void Empty_IsFalse()
    {
        Assert.False(RuleLogicEvaluator.Evaluate(RuleLogic.Any, Array.Empty<bool>(), out _));
        Assert.False(RuleLogicEvaluator.Evaluate(RuleLogic.All, Array.Empty<bool>(), out _));
    }

    [Fact]
    public void EvaluateResults_Works()
    {
        var results = new[]
        {
            RuleResult.Matched("a"),
            RuleResult.NotMatched("b")
        };
        Assert.True(RuleLogicEvaluator.EvaluateResults(RuleLogic.Any, results));
        Assert.False(RuleLogicEvaluator.EvaluateResults(RuleLogic.All, results));
    }
}
