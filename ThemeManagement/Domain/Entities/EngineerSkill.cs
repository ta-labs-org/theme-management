namespace ThemeManagement.Domain.Entities;

public class EngineerSkill
{
    public int Id { get; set; }
    public int EngineerId { get; set; }
    public Engineer Engineer { get; set; } = null!;
    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public int Level { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
