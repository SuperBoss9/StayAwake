namespace StayAwake.Models;

public sealed class ProcessRuleModel
{
    public string ProcessName { get; set; } = string.Empty;
    public int? ProcessId { get; set; }
    public bool Enabled { get; set; } = true;
    public string? DisplayName { get; set; }
}
