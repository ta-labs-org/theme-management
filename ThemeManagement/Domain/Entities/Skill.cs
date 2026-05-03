namespace ThemeManagement.Domain.Entities;

public class Skill
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<EngineerSkill> EngineerSkills { get; set; } = new List<EngineerSkill>();
    public ICollection<ThemeRequiredSkill> ThemeRequiredSkills { get; set; } = new List<ThemeRequiredSkill>();
}
