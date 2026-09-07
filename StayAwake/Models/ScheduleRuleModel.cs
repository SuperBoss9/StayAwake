namespace StayAwake.Models;

public sealed class ScheduleRuleModel
{
    public string Name { get; set; } = "Schedule";
    public bool Enabled { get; set; } = true;
    public TimeSpan StartTime { get; set; } = new(9, 0, 0);
    public TimeSpan EndTime { get; set; } = new(18, 0, 0);
    /// <summary>Bit flags: Sunday=1, Monday=2, Tuesday=4, Wednesday=8, Thursday=16, Friday=32, Saturday=64. Default Mon-Fri.</summary>
    public int DaysOfWeek { get; set; } = 0b0111110;
}
