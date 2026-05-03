namespace ThemeManagement.Domain.Entities;

public class Theme
{
    public int Id { get; set; }
    public string ThemeNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OrderType { get; set; } = "見積";
    public DateOnly OrderDate { get; set; }
    public DateOnly EstimatedCompletionDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public decimal OrderAmount { get; set; }
    public string Status { get; set; } = "Active";
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public IEnumerable<string> GetTagList() =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public ICollection<EngineerThemeAllocation> EngineerAllocations { get; set; } = new List<EngineerThemeAllocation>();
}
