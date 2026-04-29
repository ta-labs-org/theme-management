namespace ThemeManagement.Domain.Entities;

public class EngineerThemeAllocation
{
    public int Id { get; set; }
    public int EngineerId { get; set; }
    public Engineer Engineer { get; set; } = null!;
    public int ThemeId { get; set; }
    public Theme Theme { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal AllocatedHours { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
