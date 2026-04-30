using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Models;

namespace ThemeManagement.Services;

public interface IDashboardService
{
    Task<List<EngineerWorkSummaryDto>> GetEngineerSummaryAsync(int year, int month);
    Task<List<ThemeProgressDto>> GetThemeProgressAsync();
}

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICapacitySettings _capacitySettings;

    public DashboardService(AppDbContext db, ICapacitySettings capacitySettings)
    {
        _db = db;
        _capacitySettings = capacitySettings;
    }

    public async Task<List<EngineerWorkSummaryDto>> GetEngineerSummaryAsync(int year, int month)
    {
        var engineers = await _db.Engineers
            .Include(e => e.Grade)
            .Where(e => e.IsActive)
            .OrderBy(e => e.Name)
            .ToListAsync();

        var engineerIds = engineers.Select(e => e.Id).ToList();

        var adjustments = await _db.EngineerMonthlyAdjustments
            .Where(a => engineerIds.Contains(a.EngineerId) && a.Year == year && a.Month == month)
            .ToListAsync();

        var monthlyWorkDays = await _db.MonthlyWorkDays
            .FirstOrDefaultAsync(m => m.Year == year && m.Month == month);

        var allocations = await _db.EngineerThemeAllocations
            .Where(a => engineerIds.Contains(a.EngineerId) && a.Year == year && a.Month == month)
            .ToListAsync();

        return engineers.Select(e =>
        {
            var adjustment = adjustments.FirstOrDefault(a => a.EngineerId == e.Id);
            int workDays = adjustment?.WorkDays ?? monthlyWorkDays?.WorkDays ?? 0;
            var max = workDays * 8m * _capacitySettings.Coefficient;
            var total = allocations.Where(a => a.EngineerId == e.Id).Sum(a => a.AllocatedHours);
            var remaining = max - total;
            var rate = max > 0 ? total / max * 100 : 0m;
            return new EngineerWorkSummaryDto(e.Id, e.Name, e.Grade.Name, max, total, remaining, rate);
        }).ToList();
    }

    public async Task<List<ThemeProgressDto>> GetThemeProgressAsync()
    {
        var themes = await _db.Themes
            .Where(t => t.Status == "Active")
            .OrderByDescending(t => t.OrderDate)
            .ToListAsync();

        var themeIds = themes.Select(t => t.Id).ToList();

        var allAllocations = await _db.EngineerThemeAllocations
            .Include(a => a.Engineer).ThenInclude(e => e.Grade)
            .Where(a => themeIds.Contains(a.ThemeId))
            .ToListAsync();

        // 現在の半期の前期繰越を取得
        var today = DateTime.Today;
        int fiscalYear = today.Month >= 4 ? today.Year : today.Year - 1;
        bool isFirstHalf = today.Month >= 4 && today.Month <= 9;
        var carryOvers = await _db.ThemeCarryOvers
            .Where(c => c.FiscalYear == fiscalYear && c.IsFirstHalf == isFirstHalf)
            .ToListAsync();
        var carryOverMap = carryOvers.ToDictionary(c => c.ThemeId, c => c.CarryOverAmount);

        var now = DateTime.Now;
        var result = new List<ThemeProgressDto>();

        foreach (var theme in themes)
        {
            var themeAllocs = allAllocations.Where(a => a.ThemeId == theme.Id).ToList();
            bool useCost = theme.OrderType == "社用開発";
            var allocCost = themeAllocs.Sum(a => a.AllocatedHours * (useCost ? a.Engineer.Grade.UnitCostPrice : a.Engineer.Grade.UnitSalePrice));
            var carryOver = carryOverMap.GetValueOrDefault(theme.Id, 0m);
            var totalCost = allocCost + carryOver;
            var progressRate = theme.OrderAmount > 0 ? totalCost / theme.OrderAmount * 100 : 0m;
            var remaining = theme.OrderAmount - totalCost;

            int? estYear = null, estMonth = null;
            if (themeAllocs.Any() && remaining > 0)
            {
                var monthCount = themeAllocs.GroupBy(a => new { a.Year, a.Month }).Count();
                var avgMonthlyCost = allocCost / monthCount;
                if (avgMonthlyCost > 0)
                {
                    var monthsNeeded = (int)Math.Ceiling((double)(remaining / avgMonthlyCost));
                    var estimated = new DateTime(now.Year, now.Month, 1).AddMonths(monthsNeeded);
                    estYear = estimated.Year;
                    estMonth = estimated.Month;
                }
            }

            result.Add(new ThemeProgressDto(
                theme.Id, theme.Name, theme.Status,
                theme.OrderAmount, allocCost, carryOver, progressRate, remaining,
                estYear, estMonth));
        }

        return result;
    }
}
