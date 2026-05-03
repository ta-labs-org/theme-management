namespace ThemeManagement.Domain.Entities;

public class Engineer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<EngineerMonthlyAdjustment> MonthlyAdjustments { get; set; } = new List<EngineerMonthlyAdjustment>();
    public ICollection<EngineerThemeAllocation> ThemeAllocations { get; set; } = new List<EngineerThemeAllocation>();
    public ICollection<EngineerSkill> Skills { get; set; } = new List<EngineerSkill>();
}
