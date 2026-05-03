namespace ThemeManagement.Domain.Entities;

/// <summary>グレード単価の変更履歴</summary>
public class GradePriceHistory
{
    public int Id { get; set; }
    public int GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    /// <summary>この単価が有効になった日（グレード単価変更日）</summary>
    public DateOnly ValidFrom { get; set; }
    public decimal UnitSalePrice { get; set; }
    public decimal UnitCostPrice { get; set; }
    public DateTime CreatedAt { get; set; }
}
