using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

public interface IGradeService
{
    Task<List<Grade>> GetAllAsync();
    Task<Grade?> GetByIdAsync(int id);
    Task SaveAsync(Grade grade);
    Task DeleteAsync(int id);
    Task<bool> IsUsedAsync(int id);
}

public class GradeService : IGradeService
{
    private readonly AppDbContext _db;
    public GradeService(AppDbContext db) => _db = db;

    public Task<List<Grade>> GetAllAsync() =>
        _db.Grades.AsNoTracking().OrderBy(g => g.Name).ToListAsync();

    public Task<Grade?> GetByIdAsync(int id) =>
        _db.Grades.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);

    public async Task SaveAsync(Grade grade)
    {
        if (grade.Id == 0)
            _db.Grades.Add(grade);
        else
        {
            var existing = await _db.Grades.FindAsync(grade.Id);
            if (existing != null)
            {
                existing.Name = grade.Name;
                existing.UnitSalePrice = grade.UnitSalePrice;
                existing.UnitCostPrice = grade.UnitCostPrice;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var grade = await _db.Grades.FindAsync(id);
        if (grade != null)
        {
            _db.Grades.Remove(grade);
            await _db.SaveChangesAsync();
        }
    }

    public Task<bool> IsUsedAsync(int id) =>
        _db.Engineers.AnyAsync(e => e.GradeId == id);
}
