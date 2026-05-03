namespace ThemeManagement.Domain.Entities;

public class ThemeRequiredSkill
{
    public int Id { get; set; }
    public int ThemeId { get; set; }
    public Theme Theme { get; set; } = null!;
    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public int RequiredLevel { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
