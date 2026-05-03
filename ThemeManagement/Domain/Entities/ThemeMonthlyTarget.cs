namespace ThemeManagement.Domain.Entities;

/// <summary>テーマの月別合計目標稼働時間</summary>
public class ThemeMonthlyTarget
{
    public int Id { get; set; }
    public int ThemeId { get; set; }
    public Theme Theme { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>月別合計目標稼働時間 (h)</summary>
    public decimal TargetHours { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
