namespace ThemeManagement.Domain.Entities;

public class Grade
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitSalePrice { get; set; }
    public decimal UnitCostPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Engineer> Engineers { get; set; } = new List<Engineer>();
}
