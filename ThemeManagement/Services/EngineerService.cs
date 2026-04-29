using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

public interface IEngineerService
{
    Task<List<Engineer>> GetAllAsync(bool includeInactive = false);
    Task<Engineer?> GetByIdAsync(int id);
    Task SaveAsync(Engineer engineer);
    Task DeactivateAsync(int id);
}

public class EngineerService : IEngineerService
{
    private readonly AppDbContext _db;
    public EngineerService(AppDbContext db) => _db = db;

    public Task<List<Engineer>> GetAllAsync(bool includeInactive = false)
    {
        var query = _db.Engineers.Include(e => e.Grade).AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(e => e.IsActive);
        return query.OrderBy(e => e.Name).ToListAsync();
    }

    public Task<Engineer?> GetByIdAsync(int id) =>
        _db.Engineers.Include(e => e.Grade).AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

    public async Task SaveAsync(Engineer engineer)
    {
        if (engineer.Id == 0)
        {
            engineer.IsActive = true;
            _db.Engineers.Add(engineer);
        }
        else
        {
            var existing = await _db.Engineers.FindAsync(engineer.Id);
            if (existing != null)
            {
                existing.Name = engineer.Name;
                existing.GradeId = engineer.GradeId;
                existing.IsActive = engineer.IsActive;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeactivateAsync(int id)
    {
        var engineer = await _db.Engineers.FindAsync(id);
        if (engineer != null)
        {
            engineer.IsActive = false;
            await _db.SaveChangesAsync();
        }
    }
}
