using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

public interface ISkillService
{
    Task<List<Skill>> GetAllAsync();
    Task<Skill?> GetByIdAsync(int id);
    Task SaveAsync(Skill skill);
    Task DeleteAsync(int id);
    Task<bool> IsUsedAsync(int skillId);

    Task<List<EngineerSkill>> GetEngineerSkillsAsync(int engineerId);
    Task SaveEngineerSkillAsync(EngineerSkill engineerSkill);
    Task DeleteEngineerSkillAsync(int id);

    Task<List<ThemeRequiredSkill>> GetThemeRequiredSkillsAsync(int themeId);
    Task SaveThemeRequiredSkillAsync(ThemeRequiredSkill requiredSkill);
    Task DeleteThemeRequiredSkillAsync(int id);

    Task<List<EngineerSkill>> GetMatchingEngineersForThemeAsync(int themeId);
}

public class SkillService : ISkillService
{
    private readonly AppDbContext _db;
    public SkillService(AppDbContext db) => _db = db;

    public Task<List<Skill>> GetAllAsync() =>
        _db.Skills.AsNoTracking().OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync();

    public Task<Skill?> GetByIdAsync(int id) =>
        _db.Skills.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

    public async Task SaveAsync(Skill skill)
    {
        if (skill.Id == 0)
        {
            _db.Skills.Add(skill);
        }
        else
        {
            var existing = await _db.Skills.FindAsync(skill.Id);
            if (existing != null)
            {
                existing.Name = skill.Name;
                existing.Category = skill.Category;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var skill = await _db.Skills.FindAsync(id);
        if (skill != null)
        {
            _db.Skills.Remove(skill);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<bool> IsUsedAsync(int skillId) =>
        await _db.EngineerSkills.AnyAsync(es => es.SkillId == skillId) ||
        await _db.ThemeRequiredSkills.AnyAsync(ts => ts.SkillId == skillId);

    public Task<List<EngineerSkill>> GetEngineerSkillsAsync(int engineerId) =>
        _db.EngineerSkills
            .Include(es => es.Skill)
            .Where(es => es.EngineerId == engineerId)
            .OrderBy(es => es.Skill.Category)
            .ThenBy(es => es.Skill.Name)
            .AsNoTracking()
            .ToListAsync();

    public async Task SaveEngineerSkillAsync(EngineerSkill engineerSkill)
    {
        var existing = await _db.EngineerSkills
            .FirstOrDefaultAsync(es => es.EngineerId == engineerSkill.EngineerId && es.SkillId == engineerSkill.SkillId);
        if (existing != null)
        {
            existing.Level = engineerSkill.Level;
        }
        else
        {
            _db.EngineerSkills.Add(engineerSkill);
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteEngineerSkillAsync(int id)
    {
        var es = await _db.EngineerSkills.FindAsync(id);
        if (es != null)
        {
            _db.EngineerSkills.Remove(es);
            await _db.SaveChangesAsync();
        }
    }

    public Task<List<ThemeRequiredSkill>> GetThemeRequiredSkillsAsync(int themeId) =>
        _db.ThemeRequiredSkills
            .Include(ts => ts.Skill)
            .Where(ts => ts.ThemeId == themeId)
            .OrderBy(ts => ts.Skill.Category)
            .ThenBy(ts => ts.Skill.Name)
            .AsNoTracking()
            .ToListAsync();

    public async Task SaveThemeRequiredSkillAsync(ThemeRequiredSkill requiredSkill)
    {
        var existing = await _db.ThemeRequiredSkills
            .FirstOrDefaultAsync(ts => ts.ThemeId == requiredSkill.ThemeId && ts.SkillId == requiredSkill.SkillId);
        if (existing != null)
        {
            existing.RequiredLevel = requiredSkill.RequiredLevel;
        }
        else
        {
            _db.ThemeRequiredSkills.Add(requiredSkill);
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteThemeRequiredSkillAsync(int id)
    {
        var ts = await _db.ThemeRequiredSkills.FindAsync(id);
        if (ts != null)
        {
            _db.ThemeRequiredSkills.Remove(ts);
            await _db.SaveChangesAsync();
        }
    }

    public Task<List<EngineerSkill>> GetMatchingEngineersForThemeAsync(int themeId) =>
        _db.EngineerSkills
            .Include(es => es.Engineer)
            .Include(es => es.Skill)
            .Where(es => _db.ThemeRequiredSkills
                .Any(ts => ts.ThemeId == themeId && ts.SkillId == es.SkillId && es.Level >= ts.RequiredLevel))
            .AsNoTracking()
            .ToListAsync();
}
