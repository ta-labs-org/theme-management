using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

public interface IReportPdfService
{
    Task<byte[]> GenerateDashboardPdfAsync(int year, int month);
    Task<byte[]> GenerateForecastPdfAsync(int fiscalYear, bool isFirstHalf);
}

/// <summary>
/// QuestPDF を使用して各種レポートの PDF を生成するサービス。
/// 日本語テキストを正しくレンダリングするには、サーバーに日本語フォント
/// （例: fonts-noto-cjk）がインストールされている必要があります。
/// </summary>
public class ReportPdfService : IReportPdfService
{
    private readonly IDashboardService _dashboardService;
    private readonly IEngineerService _engineerService;
    private readonly IThemeService _themeService;
    private readonly IAllocationService _allocationService;
    private readonly IWorkDayService _workDayService;
    private readonly ICapacitySettings _capacitySettings;
    private readonly AppDbContext _db;

    public ReportPdfService(
        IDashboardService dashboardService,
        IEngineerService engineerService,
        IThemeService themeService,
        IAllocationService allocationService,
        IWorkDayService workDayService,
        ICapacitySettings capacitySettings,
        AppDbContext db)
    {
        _dashboardService = dashboardService;
        _engineerService = engineerService;
        _themeService = themeService;
        _allocationService = allocationService;
        _workDayService = workDayService;
        _capacitySettings = capacitySettings;
        _db = db;
    }

    public async Task<byte[]> GenerateDashboardPdfAsync(int year, int month)
    {
        var engineers = await _dashboardService.GetEngineerSummaryAsync(year, month);
        var themes = await _dashboardService.GetThemeProgressAsync();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().Text($"ダッシュボード レポート ({year}年{month}月)")
                        .SemiBold().FontSize(16);
                    col.Item().Text($"出力日時: {DateTime.Now:yyyy/MM/dd HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingBottom(8);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(12);

                    // ── エンジニア稼働サマリ ──
                    col.Item().Text("エンジニア稼働サマリ").SemiBold().FontSize(13);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            foreach (var label in new[] { "エンジニア", "等級", "最大開発可能(h)", "割り当て合計(h)", "残余(h)", "稼働率(%)" })
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .AlignCenter().Text(label).FontColor(Colors.White).FontSize(9);
                        });

                        bool alt = false;
                        foreach (var eng in engineers)
                        {
                            var isOver = eng.MaxDevelopableHours > 0 && eng.TotalAllocatedHours > eng.MaxDevelopableHours;
                            var bg = isOver ? Colors.Red.Lighten4 : alt ? Colors.Grey.Lighten4 : Colors.White;

                            table.Cell().Background(bg).Padding(4).Text(eng.EngineerName).FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignCenter().Text(eng.GradeName).FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text(eng.MaxDevelopableHours.ToString("F1")).FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text(eng.TotalAllocatedHours.ToString("F1")).FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text(eng.RemainingHours.ToString("F1")).FontSize(9)
                                .FontColor(eng.RemainingHours < 0 ? Colors.Red.Darken2 : Colors.Black);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text($"{eng.WorkRate:F1}%").FontSize(9)
                                .FontColor(isOver ? Colors.Red.Darken2 : Colors.Black);
                            alt = !alt;
                        }
                    });

                    // ── テーマ進捗サマリ ──
                    col.Item().Text("テーマ進捗サマリ（アクティブ案件）").SemiBold().FontSize(13);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            foreach (var label in new[] { "テーマ名", "受注金額(円)", "稼働コスト(円)", "前期繰越(円)", "合計コスト(円)", "消化率(%)", "残余金額(円)", "完了見込み" })
                                header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                    .AlignCenter().Text(label).FontColor(Colors.White).FontSize(9);
                        });

                        bool alt = false;
                        foreach (var theme in themes)
                        {
                            var isOver = theme.ProgressRate > 100;
                            var bg = isOver ? Colors.Red.Lighten4 : alt ? Colors.Grey.Lighten4 : Colors.White;
                            var totalCost = theme.TotalAllocatedCost + theme.CarryOverAmount;

                            table.Cell().Background(bg).Padding(4).Text(theme.ThemeName).FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text(theme.OrderAmount.ToString("N0")).FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text(theme.TotalAllocatedCost.ToString("N0")).FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text(theme.CarryOverAmount > 0 ? theme.CarryOverAmount.ToString("N0") : "—").FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text(totalCost.ToString("N0")).SemiBold().FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text($"{theme.ProgressRate:F1}%").FontSize(9)
                                .FontColor(isOver ? Colors.Red.Darken2 : Colors.Black);
                            table.Cell().Background(bg).Padding(4).AlignRight()
                                .Text(theme.RemainingAmount.ToString("N0")).FontSize(9);
                            table.Cell().Background(bg).Padding(4).AlignCenter()
                                .Text(theme.EstimatedCompletionYear.HasValue
                                    ? $"{theme.EstimatedCompletionYear}年{theme.EstimatedCompletionMonth}月"
                                    : "—").FontSize(9);
                            alt = !alt;
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("ページ ").FontSize(9);
                    x.CurrentPageNumber().FontSize(9);
                    x.Span(" / ").FontSize(9);
                    x.TotalPages().FontSize(9);
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> GenerateForecastPdfAsync(int fiscalYear, bool isFirstHalf)
    {
        // 対象期間の月リストを構築
        var periodMonths = isFirstHalf
            ? Enumerable.Range(4, 6).Select(m => (Year: fiscalYear, Month: m)).ToList()
            : new List<(int Year, int Month)>
              {
                  (fiscalYear, 10), (fiscalYear, 11), (fiscalYear, 12),
                  (fiscalYear + 1, 1), (fiscalYear + 1, 2), (fiscalYear + 1, 3)
              };

        var engineers = await _engineerService.GetAllAsync();
        var themes = await _themeService.GetAllAsync(activeOnly: true);

        // 割り当てデータ
        var allocMap = new Dictionary<(int EngId, int ThemeId, int Year, int Month), decimal>();
        foreach (var grp in periodMonths.GroupBy(p => p.Year))
        {
            var allocs = await _allocationService.GetAllocationsForPeriodAsync(
                grp.Key, grp.Select(p => p.Month));
            foreach (var a in allocs)
                allocMap[(a.EngineerId, a.ThemeId, a.Year, a.Month)] = a.AllocatedHours;
        }

        // 前期繰越データ
        var carryOvers = await _db.ThemeCarryOvers
            .Where(c => c.FiscalYear == fiscalYear && c.IsFirstHalf == isFirstHalf)
            .ToListAsync();
        var carryOverMap = carryOvers.ToDictionary(c => c.ThemeId, c => c.CarryOverAmount);

        // 稼働日数データ
        var baseWorkDays = new Dictionary<(int, int), int>();
        var adjustmentWorkDays = new Dictionary<(int, int, int), int>();
        foreach (var (y, m) in periodMonths)
        {
            var wd = await _workDayService.GetByYearMonthAsync(y, m);
            if (wd != null) baseWorkDays[(y, m)] = wd.WorkDays;
            var adjs = await _workDayService.GetAdjustmentsAsync(y, m);
            foreach (var adj in adjs)
                adjustmentWorkDays[(adj.EngineerId, y, m)] = adj.WorkDays;
        }

        decimal GetAlloc(int engId, int themeId, int y, int m) =>
            allocMap.TryGetValue((engId, themeId, y, m), out var v) ? v : 0;

        decimal GetCapacity(int engId, int y, int m)
        {
            int days = adjustmentWorkDays.TryGetValue((engId, y, m), out var adj)
                ? adj
                : baseWorkDays.TryGetValue((y, m), out var baseWd) ? baseWd : 0;
            return days * 8m * _capacitySettings.Coefficient;
        }

        decimal GetEngineerMonthTotal(int engId, int y, int m) =>
            themes.Sum(t => GetAlloc(engId, t.Id, y, m));

        decimal GetThemeMonthTotal(int themeId, int y, int m) =>
            engineers.Sum(e => GetAlloc(e.Id, themeId, y, m));

        static string FmtH(decimal h) => h == 0 ? "-" : $"{h:F0}h";

        var halfLabel = isFirstHalf ? "上期" : "下期";
        // total columns: 2 fixed (engineer + theme) + N months + 1 total
        int totalColumns = 2 + periodMonths.Count + 1;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text($"半期見込計算 ({fiscalYear}年度 {halfLabel})")
                        .SemiBold().FontSize(16);
                    col.Item().Text($"出力日時: {DateTime.Now:yyyy/MM/dd HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingBottom(6);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(14);

                    // ── エンジニアビュー ──
                    col.Item().Text("エンジニアビュー").SemiBold().FontSize(12);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2f);  // engineer
                            columns.RelativeColumn(2f);  // theme
                            foreach (var _ in periodMonths) columns.RelativeColumn(1f);
                            columns.RelativeColumn(1f);  // total
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                .Text("エンジニア").FontColor(Colors.White).FontSize(8);
                            header.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                .Text("テーマ").FontColor(Colors.White).FontSize(8);
                            foreach (var (_, m) in periodMonths)
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                    .AlignCenter().Text($"{m}月").FontColor(Colors.White).FontSize(8);
                            header.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                .AlignCenter().Text("合計").FontColor(Colors.White).FontSize(8);
                        });

                        foreach (var eng in engineers)
                        {
                            bool firstTheme = true;
                            foreach (var theme in themes)
                            {
                                var rowBg = firstTheme ? Colors.White : Colors.Grey.Lighten5;

                                // Engineer name cell (only for first theme)
                                if (firstTheme)
                                {
                                    table.Cell().Background(rowBg).Padding(3)
                                        .Text(eng.Name).SemiBold().FontSize(8);
                                }
                                else
                                {
                                    table.Cell().Background(rowBg).Padding(3)
                                        .Text("").FontSize(8);
                                }

                                table.Cell().Background(rowBg).Padding(3)
                                    .Text(theme.Name).FontSize(8);

                                foreach (var (y, m) in periodMonths)
                                {
                                    var h = GetAlloc(eng.Id, theme.Id, y, m);
                                    table.Cell().Background(rowBg).Padding(3)
                                        .AlignCenter().Text(FmtH(h)).FontSize(8);
                                }

                                var rowTotal = periodMonths.Sum(p => GetAlloc(eng.Id, theme.Id, p.Year, p.Month));
                                table.Cell().Background(Colors.Green.Lighten4).Padding(3)
                                    .AlignCenter().Text(FmtH(rowTotal)).SemiBold().FontSize(8);
                                firstTheme = false;
                            }

                            // Subtotal row
                            table.Cell().ColumnSpan(2).Background(Colors.Yellow.Lighten2).Padding(4)
                                .Text("小計").SemiBold().FontSize(8);
                            foreach (var (y, m) in periodMonths)
                            {
                                var total = GetEngineerMonthTotal(eng.Id, y, m);
                                var cap = GetCapacity(eng.Id, y, m);
                                var isOver = cap > 0 && total > cap;
                                table.Cell().Background(isOver ? Colors.Red.Lighten3 : Colors.Yellow.Lighten2)
                                    .Padding(4).AlignCenter()
                                    .Text(FmtH(total) + (isOver ? " !" : "")).SemiBold().FontSize(8);
                            }
                            var engTotal = periodMonths.Sum(p => GetEngineerMonthTotal(eng.Id, p.Year, p.Month));
                            table.Cell().Background(Colors.Yellow.Lighten2).Padding(4)
                                .AlignCenter().Text(FmtH(engTotal)).SemiBold().FontSize(8);

                            // Capacity row
                            table.Cell().ColumnSpan(2).Background(Colors.Cyan.Lighten4).Padding(3)
                                .Text("稼働上限").FontSize(7).Italic();
                            foreach (var (y, m) in periodMonths)
                            {
                                var cap = GetCapacity(eng.Id, y, m);
                                table.Cell().Background(Colors.Cyan.Lighten4).Padding(3)
                                    .AlignCenter().Text(cap > 0 ? $"{cap:F0}h" : "—").FontSize(7).Italic();
                            }
                            table.Cell().Background(Colors.Cyan.Lighten4).Padding(3).Text("").FontSize(7);
                        }
                    });

                    // ── テーマビュー ──
                    col.Item().PageBreak();
                    col.Item().Text("テーマビュー").SemiBold().FontSize(12);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.5f); // theme
                            columns.RelativeColumn(2f);   // engineer
                            foreach (var _ in periodMonths) columns.RelativeColumn(1f);
                            columns.RelativeColumn(1f);   // total
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                .Text("テーマ").FontColor(Colors.White).FontSize(8);
                            header.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                .Text("エンジニア").FontColor(Colors.White).FontSize(8);
                            foreach (var (_, m) in periodMonths)
                                header.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                    .AlignCenter().Text($"{m}月").FontColor(Colors.White).FontSize(8);
                            header.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                .AlignCenter().Text("合計").FontColor(Colors.White).FontSize(8);
                        });

                        foreach (var theme in themes)
                        {
                            bool firstEng = true;
                            foreach (var eng in engineers)
                            {
                                var rowBg = firstEng ? Colors.White : Colors.Grey.Lighten5;

                                // Theme info cell (only for first engineer)
                                if (firstEng)
                                {
                                    table.Cell().Background(rowBg).Padding(3).Column(c =>
                                    {
                                        c.Item().Text(theme.Name).SemiBold().FontSize(8);
                                        c.Item().Text($"受注: ¥{theme.OrderAmount:N0}").FontSize(7)
                                            .FontColor(Colors.Blue.Darken2);
                                        c.Item().Text($"〆 {theme.EstimatedCompletionDate:yyyy/M}").FontSize(7);
                                    });
                                }
                                else
                                {
                                    table.Cell().Background(rowBg).Padding(3).Text("").FontSize(8);
                                }

                                table.Cell().Background(rowBg).Padding(3)
                                    .Text(eng.Name).FontSize(8);

                                foreach (var (y, m) in periodMonths)
                                {
                                    var h = GetAlloc(eng.Id, theme.Id, y, m);
                                    table.Cell().Background(rowBg).Padding(3)
                                        .AlignCenter().Text(FmtH(h)).FontSize(8);
                                }

                                var rowTotal = periodMonths.Sum(p => GetAlloc(eng.Id, theme.Id, p.Year, p.Month));
                                table.Cell().Background(Colors.Green.Lighten4).Padding(3)
                                    .AlignCenter().Text(FmtH(rowTotal)).SemiBold().FontSize(8);
                                firstEng = false;
                            }

                            // Subtotal row
                            table.Cell().ColumnSpan(2).Background(Colors.Yellow.Lighten2).Padding(4)
                                .Text("小計（工数）").SemiBold().FontSize(8);
                            foreach (var (y, m) in periodMonths)
                            {
                                var total = GetThemeMonthTotal(theme.Id, y, m);
                                table.Cell().Background(Colors.Yellow.Lighten2).Padding(4)
                                    .AlignCenter().Text(FmtH(total)).SemiBold().FontSize(8);
                            }
                            var themeTotal = periodMonths.Sum(p => GetThemeMonthTotal(theme.Id, p.Year, p.Month));
                            table.Cell().Background(Colors.Yellow.Lighten2).Padding(4)
                                .AlignCenter().Text(FmtH(themeTotal)).SemiBold().FontSize(8);

                            // Cost row
                            var carryOver = carryOverMap.GetValueOrDefault(theme.Id, 0m);
                            bool useCost = theme.OrderType == "社用開発";
                            decimal periodCost = engineers.Sum(e =>
                                periodMonths.Sum(p =>
                                {
                                    var h = GetAlloc(e.Id, theme.Id, p.Year, p.Month);
                                    if (h <= 0) return 0m;
                                    return h * (useCost ? (e.Grade?.UnitCostPrice ?? 0) : (e.Grade?.UnitSalePrice ?? 0));
                                }));
                            var totalCost = periodCost + carryOver;
                            var isOverBudget = theme.OrderAmount > 0 && totalCost > theme.OrderAmount;
                            var costBg = isOverBudget ? Colors.Red.Lighten3 : Colors.Green.Lighten4;
                            var costText = $"コスト試算: ¥{periodCost:N0}" +
                                (carryOver > 0 ? $" + 繰越 ¥{carryOver:N0} = ¥{totalCost:N0}" : "") +
                                $" / 受注 ¥{theme.OrderAmount:N0}" +
                                (isOverBudget ? " [超過]" : "");
                            table.Cell().ColumnSpan((uint)totalColumns).Background(costBg).Padding(5)
                                .Text(costText).SemiBold().FontSize(8)
                                .FontColor(isOverBudget ? Colors.Red.Darken2 : Colors.Green.Darken2);
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("ページ ").FontSize(9);
                    x.CurrentPageNumber().FontSize(9);
                    x.Span(" / ").FontSize(9);
                    x.TotalPages().FontSize(9);
                });
            });
        });

        return document.GeneratePdf();
    }
}
