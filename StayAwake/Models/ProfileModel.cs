namespace StayAwake.Models;

public sealed class ProfileModel
{
    public string Name { get; set; } = "Default";
    public AwakeMode Mode { get; set; } = AwakeMode.Indefinite;
    public bool KeepDisplayOn { get; set; }
    public RuleLogic RuleLogic { get; set; } = RuleLogic.Any;
    public int TimedDurationMinutes { get; set; } = 60;
    public string UntilTime { get; set; } = "22:00";
    public bool ProcessRuleEnabled { get; set; }
    public bool CpuRuleEnabled { get; set; }
    public bool NetworkRuleEnabled { get; set; }
    public bool ScheduleRuleEnabled { get; set; }
}
