using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

public interface IThemeService
{
    Task<List<Theme>> GetAllAsync(bool activeOnly = false);
    Task<Theme?> GetByIdAsync(int id);
    Task SaveAsync(Theme theme);
    Task<(int Added, int Updated)> BulkImportAsync(IEnumerable<Theme> themes);
}

public class ThemeService : IThemeService
{
    private readonly AppDbContext _db;
    public ThemeService(AppDbContext db) => _db = db;

    public Task<List<Theme>> GetAllAsync(bool activeOnly = false)
    {
        var query = _db.Themes.AsNoTracking().AsQueryable();
        if (activeOnly)
            query = query.Where(t => ThemeStatus.ActiveStatuses.Contains(t.Status));
        return query.OrderByDescending(t => t.OrderDate).ToListAsync();
    }

    public Task<Theme?> GetByIdAsync(int id) =>
        _db.Themes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);

    public async Task SaveAsync(Theme theme)
    {
        if (theme.Id == 0)
            _db.Themes.Add(theme);
        else
        {
            var existing = await _db.Themes.FindAsync(theme.Id);
            if (existing != null)
            {
                existing.ThemeNo = theme.ThemeNo;
                existing.Name = theme.Name;
                existing.OrderType = theme.OrderType;
                existing.OrderDate = theme.OrderDate;
                existing.EstimatedCompletionDate = theme.EstimatedCompletionDate;
                existing.ActualCompletionDate = theme.ActualCompletionDate;
                existing.OrderAmount = theme.OrderAmount;
                existing.Status = theme.Status;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task<(int Added, int Updated)> BulkImportAsync(IEnumerable<Theme> themes)
    {
        int added = 0, updated = 0;
        var existingByNo = await _db.Themes.ToDictionaryAsync(t => t.ThemeNo);

        foreach (var theme in themes)
        {
            if (existingByNo.TryGetValue(theme.ThemeNo, out var existing))
            {
                existing.Name = theme.Name;
                existing.OrderType = theme.OrderType;
                existing.OrderDate = theme.OrderDate;
                existing.EstimatedCompletionDate = theme.EstimatedCompletionDate;
                existing.ActualCompletionDate = theme.ActualCompletionDate;
                existing.OrderAmount = theme.OrderAmount;
                existing.Status = theme.Status;
                updated++;
            }
            else
            {
                _db.Themes.Add(theme);
                added++;
            }
        }
        await _db.SaveChangesAsync();
        return (added, updated);
    }
}
