namespace StayAwake.Models;

public sealed class RuleResult
{
    public bool IsMatched { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime LastChanged { get; init; } = DateTime.UtcNow;
    public string? OptionalDetails { get; init; }

    public static RuleResult Matched(string description, string? details = null) =>
        new() { IsMatched = true, Description = description, OptionalDetails = details, LastChanged = DateTime.UtcNow };

    public static RuleResult NotMatched(string description, string? details = null) =>
        new() { IsMatched = false, Description = description, OptionalDetails = details, LastChanged = DateTime.UtcNow };
}
