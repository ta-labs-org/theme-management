using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

public interface IWorkDayService
{
    Task<List<MonthlyWorkDays>> GetAllAsync();
    Task<MonthlyWorkDays?> GetByYearMonthAsync(int year, int month);
    Task SaveAsync(MonthlyWorkDays workDays);
    Task DeleteAsync(int id);
    Task<List<EngineerMonthlyAdjustment>> GetAdjustmentsAsync(int year, int month);
    Task<EngineerMonthlyAdjustment?> GetAdjustmentAsync(int engineerId, int year, int month);
    Task SaveAdjustmentAsync(EngineerMonthlyAdjustment adjustment);
    Task DeleteAdjustmentAsync(int id);
}

public class WorkDayService : IWorkDayService
{
    private readonly AppDbContext _db;
    public WorkDayService(AppDbContext db) => _db = db;

    public Task<List<MonthlyWorkDays>> GetAllAsync() =>
        _db.MonthlyWorkDays
            .AsNoTracking()
            .OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
            .ToListAsync();

    public Task<MonthlyWorkDays?> GetByYearMonthAsync(int year, int month) =>
        _db.MonthlyWorkDays.AsNoTracking().FirstOrDefaultAsync(m => m.Year == year && m.Month == month);

    public async Task SaveAsync(MonthlyWorkDays workDays)
    {
        if (workDays.Id == 0)
            _db.MonthlyWorkDays.Add(workDays);
        else
        {
            var existing = await _db.MonthlyWorkDays.FindAsync(workDays.Id);
            if (existing != null)
                existing.WorkDays = workDays.WorkDays;
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _db.MonthlyWorkDays.FindAsync(id);
        if (item != null)
        {
            _db.MonthlyWorkDays.Remove(item);
            await _db.SaveChangesAsync();
        }
    }

    public Task<List<EngineerMonthlyAdjustment>> GetAdjustmentsAsync(int year, int month) =>
        _db.EngineerMonthlyAdjustments
            .AsNoTracking()
            .Include(a => a.Engineer)
            .Where(a => a.Year == year && a.Month == month)
            .OrderBy(a => a.Engineer.Name)
            .ToListAsync();

    public Task<EngineerMonthlyAdjustment?> GetAdjustmentAsync(int engineerId, int year, int month) =>
        _db.EngineerMonthlyAdjustments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EngineerId == engineerId && a.Year == year && a.Month == month);

    public async Task SaveAdjustmentAsync(EngineerMonthlyAdjustment adjustment)
    {
        if (adjustment.Id == 0)
            _db.EngineerMonthlyAdjustments.Add(adjustment);
        else
        {
            var existing = await _db.EngineerMonthlyAdjustments.FindAsync(adjustment.Id);
            if (existing != null)
            {
                existing.WorkDays = adjustment.WorkDays;
                existing.Note = adjustment.Note;
            }
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAdjustmentAsync(int id)
    {
        var item = await _db.EngineerMonthlyAdjustments.FindAsync(id);
        if (item != null)
        {
            _db.EngineerMonthlyAdjustments.Remove(item);
            await _db.SaveChangesAsync();
        }
    }
}
