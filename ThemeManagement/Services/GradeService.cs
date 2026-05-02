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
    Task<List<GradePriceHistory>> GetPriceHistoriesAsync(int gradeId);
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
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                _db.Grades.Add(grade);
                await _db.SaveChangesAsync();
                // 新規作成時の初期単価を履歴に記録
                _db.GradePriceHistories.Add(new GradePriceHistory
                {
                    GradeId = grade.Id,
                    ValidFrom = DateOnly.FromDateTime(DateTime.Today),
                    UnitSalePrice = grade.UnitSalePrice,
                    UnitCostPrice = grade.UnitCostPrice
                });
                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        else
        {
            var existing = await _db.Grades.FindAsync(grade.Id);
            if (existing != null)
            {
                bool priceChanged = existing.UnitSalePrice != grade.UnitSalePrice
                                 || existing.UnitCostPrice != grade.UnitCostPrice;
                existing.Name = grade.Name;
                existing.UnitSalePrice = grade.UnitSalePrice;
                existing.UnitCostPrice = grade.UnitCostPrice;

                if (priceChanged)
                {
                    // 単価変更時に履歴を追加
                    _db.GradePriceHistories.Add(new GradePriceHistory
                    {
                        GradeId = grade.Id,
                        ValidFrom = DateOnly.FromDateTime(DateTime.Today),
                        UnitSalePrice = grade.UnitSalePrice,
                        UnitCostPrice = grade.UnitCostPrice
                    });
                }
            }
            await _db.SaveChangesAsync();
        }
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

    public Task<List<GradePriceHistory>> GetPriceHistoriesAsync(int gradeId) =>
        _db.GradePriceHistories
            .AsNoTracking()
            .Where(h => h.GradeId == gradeId)
            .OrderByDescending(h => h.ValidFrom)
            .ThenByDescending(h => h.Id)
            .ToListAsync();
}
