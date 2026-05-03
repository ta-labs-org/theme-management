using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;
using ThemeManagement.Domain.Exceptions;
using ThemeManagement.Models;

namespace ThemeManagement.Services;

public interface IAllocationService
{
    Task<List<AllocationRowDto>> GetByEngineerAsync(int engineerId, int year, int month);
    Task<List<AllocationRowDto>> GetByThemeAsync(int themeId, int year, int month);
    Task<List<EngineerThemeAllocation>> GetAllocationsForPeriodAsync(int year, IEnumerable<int> months);
    decimal GetMaxDevelopableHours(int engineerId, int year, int month);
    decimal GetTotalAllocatedHoursByEngineer(int engineerId, int year, int month, int? excludeId = null);
    decimal GetTotalAllocatedCostByTheme(int themeId, int? excludeId = null);
    Task UpsertAllocationAsync(int engineerId, int themeId, int year, int month, decimal hours);
    Task UpsertAllocationNoValidationAsync(int engineerId, int themeId, int year, int month, decimal hours);
    Task DeleteAllocationAsync(int id);
    Task<(int added, int updated)> BulkImportAllocationsAsync(IEnumerable<AllocationImportData> rows);
    /// <summary>前月の割当てを指定月に一括コピーします（既存データは上書き）</summary>
    Task<int> CopyFromPreviousMonthAsync(int themeId, int year, int month);
}

public class AllocationService : IAllocationService
{
    private readonly AppDbContext _db;
    private readonly ICapacitySettings _capacitySettings;
    private readonly INotificationService _notificationService;

    public AllocationService(AppDbContext db, ICapacitySettings capacitySettings, INotificationService notificationService)
    {
        _db = db;
        _capacitySettings = capacitySettings;
        _notificationService = notificationService;
    }

    public Task<List<AllocationRowDto>> GetByEngineerAsync(int engineerId, int year, int month) =>
        _db.EngineerThemeAllocations
            .Include(a => a.Engineer).ThenInclude(e => e.Grade)
            .Include(a => a.Theme)
            .Where(a => a.EngineerId == engineerId && a.Year == year && a.Month == month)
            .Select(a => new AllocationRowDto(
                a.Id, a.EngineerId, a.Engineer.Name, a.Engineer.GradeId,
                a.Engineer.Grade.Name, a.Engineer.Grade.UnitSalePrice,
                a.ThemeId, a.Theme.Name, a.Year, a.Month, a.AllocatedHours))
            .ToListAsync();

    public Task<List<AllocationRowDto>> GetByThemeAsync(int themeId, int year, int month) =>
        _db.EngineerThemeAllocations
            .Include(a => a.Engineer).ThenInclude(e => e.Grade)
            .Include(a => a.Theme)
            .Where(a => a.ThemeId == themeId && a.Year == year && a.Month == month)
            .Select(a => new AllocationRowDto(
                a.Id, a.EngineerId, a.Engineer.Name, a.Engineer.GradeId,
                a.Engineer.Grade.Name, a.Engineer.Grade.UnitSalePrice,
                a.ThemeId, a.Theme.Name, a.Year, a.Month, a.AllocatedHours))
            .ToListAsync();

    public decimal GetMaxDevelopableHours(int engineerId, int year, int month)
    {
        var adjustment = _db.EngineerMonthlyAdjustments
            .AsNoTracking()
            .FirstOrDefault(a => a.EngineerId == engineerId && a.Year == year && a.Month == month);

        int workDays = adjustment?.WorkDays
            ?? _db.MonthlyWorkDays.AsNoTracking().FirstOrDefault(m => m.Year == year && m.Month == month)?.WorkDays
            ?? 0;

        return workDays * 8m * _capacitySettings.Coefficient;
    }

    public decimal GetTotalAllocatedHoursByEngineer(int engineerId, int year, int month, int? excludeId = null)
    {
        var query = _db.EngineerThemeAllocations
            .Where(a => a.EngineerId == engineerId && a.Year == year && a.Month == month);
        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);
        return query.Sum(a => (decimal?)a.AllocatedHours) ?? 0m;
    }

    public decimal GetTotalAllocatedCostByTheme(int themeId, int? excludeId = null)
    {
        var query = _db.EngineerThemeAllocations
            .AsNoTracking()
            .Include(a => a.Engineer).ThenInclude(e => e.Grade)
            .Where(a => a.ThemeId == themeId);
        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);
        return query.AsEnumerable().Sum(a => a.AllocatedHours * a.Engineer.Grade.UnitSalePrice);
    }

    public async Task UpsertAllocationAsync(int engineerId, int themeId, int year, int month, decimal hours)
    {
        if (hours <= 0)
            throw new BusinessRuleException("割り当て時間は0より大きい値を入力してください");

        var existing = await _db.EngineerThemeAllocations
            .FirstOrDefaultAsync(a => a.EngineerId == engineerId && a.ThemeId == themeId
                                      && a.Year == year && a.Month == month);

        int? excludeId = existing?.Id;

        // エンジニア稼働上限チェック
        var maxHours = GetMaxDevelopableHours(engineerId, year, month);
        var currentEngTotal = GetTotalAllocatedHoursByEngineer(engineerId, year, month, excludeId);
        if (currentEngTotal + hours > maxHours)
        {
            var engineer = await _db.Engineers.FindAsync(engineerId);
            throw new BusinessRuleException(
                $"{engineer?.Name} の {year}/{month:D2} 稼働上限（{maxHours:F1}h）を超えます（入力後合計: {currentEngTotal + hours:F1}h）");
        }

        // テーマ受注金額チェック
        var theme = await _db.Themes.FindAsync(themeId)
            ?? throw new BusinessRuleException("テーマが見つかりません");
        var eng = await _db.Engineers.AsNoTracking().Include(e => e.Grade).FirstAsync(e => e.Id == engineerId);
        var addedCost = hours * eng.Grade.UnitSalePrice;
        var currentCost = GetTotalAllocatedCostByTheme(themeId, excludeId);
        if (currentCost + addedCost > theme.OrderAmount)
        {
            throw new BusinessRuleException(
                $"{theme.Name} の受注金額（{theme.OrderAmount:N0}円）を超えます（入力後累計: {currentCost + addedCost:N0}円）");
        }

        if (existing == null)
        {
            _db.EngineerThemeAllocations.Add(new EngineerThemeAllocation
            {
                EngineerId = engineerId,
                ThemeId = themeId,
                Year = year,
                Month = month,
                AllocatedHours = hours
            });
        }
        else
        {
            existing.AllocatedHours = hours;
        }

        await _db.SaveChangesAsync();
        await _notificationService.TrySendRealtimeAlertsAsync();
    }

    public async Task DeleteAllocationAsync(int id)
    {
        var allocation = await _db.EngineerThemeAllocations.FindAsync(id);
        if (allocation != null)
        {
            _db.EngineerThemeAllocations.Remove(allocation);
            await _db.SaveChangesAsync();
        }
    }

    public Task<List<EngineerThemeAllocation>> GetAllocationsForPeriodAsync(int year, IEnumerable<int> months)
    {
        var monthList = months.ToList();
        return _db.EngineerThemeAllocations
            .AsNoTracking()
            .Where(a => a.Year == year && monthList.Contains(a.Month))
            .ToListAsync();
    }

    public async Task UpsertAllocationNoValidationAsync(int engineerId, int themeId, int year, int month, decimal hours)
    {
        var existing = await _db.EngineerThemeAllocations
            .FirstOrDefaultAsync(a => a.EngineerId == engineerId && a.ThemeId == themeId
                                      && a.Year == year && a.Month == month);
        if (hours <= 0)
        {
            if (existing != null)
            {
                _db.EngineerThemeAllocations.Remove(existing);
                await _db.SaveChangesAsync();
            }
            return;
        }

        if (existing == null)
        {
            _db.EngineerThemeAllocations.Add(new EngineerThemeAllocation
            {
                EngineerId = engineerId,
                ThemeId = themeId,
                Year = year,
                Month = month,
                AllocatedHours = hours
            });
        }
        else
        {
            existing.AllocatedHours = hours;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<(int added, int updated)> BulkImportAllocationsAsync(IEnumerable<AllocationImportData> rows)
    {
        int added = 0, updated = 0;

        foreach (var row in rows)
        {
            var existing = await _db.EngineerThemeAllocations
                .FirstOrDefaultAsync(a => a.EngineerId == row.EngineerId && a.ThemeId == row.ThemeId
                                          && a.Year == row.Year && a.Month == row.Month);
            if (existing == null)
            {
                _db.EngineerThemeAllocations.Add(new EngineerThemeAllocation
                {
                    EngineerId = row.EngineerId,
                    ThemeId = row.ThemeId,
                    Year = row.Year,
                    Month = row.Month,
                    AllocatedHours = row.Hours
                });
                added++;
            }
            else
            {
                existing.AllocatedHours = row.Hours;
                updated++;
            }
        }

        await _db.SaveChangesAsync();
        return (added, updated);
    }

    public async Task<int> CopyFromPreviousMonthAsync(int themeId, int year, int month)
    {
        // 前月の計算
        var prevDate = new DateTime(year, month, 1).AddMonths(-1);
        int prevYear = prevDate.Year;
        int prevMonth = prevDate.Month;

        var prevAllocations = await _db.EngineerThemeAllocations
            .Where(a => a.ThemeId == themeId && a.Year == prevYear && a.Month == prevMonth)
            .ToListAsync();

        if (prevAllocations.Count == 0) return 0;

        await using var transaction = await _db.Database.BeginTransactionAsync();

        int copied = 0;
        try
        {
            foreach (var prev in prevAllocations)
            {
                await UpsertAllocationAsync(prev.EngineerId, themeId, year, month, prev.AllocatedHours);
                copied++;
            }

            await transaction.CommitAsync();
            return copied;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
