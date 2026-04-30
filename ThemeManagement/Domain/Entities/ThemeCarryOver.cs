namespace ThemeManagement.Domain.Entities;

/// <summary>テーマの前期繰越金額</summary>
public class ThemeCarryOver
{
    public int Id { get; set; }
    public int ThemeId { get; set; }
    public Theme Theme { get; set; } = null!;
    /// <summary>見込計算の年度</summary>
    public int FiscalYear { get; set; }
    /// <summary>true = 上期(4〜9月), false = 下期(10〜3月)</summary>
    public bool IsFirstHalf { get; set; }
    /// <summary>繰越金額 (円)</summary>
    public decimal CarryOverAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
