namespace ThemeManagement.Domain.Entities;

public class EngineerMonthlyAdjustment
{
    public int Id { get; set; }
    public int EngineerId { get; set; }
    public Engineer Engineer { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public int WorkDays { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
