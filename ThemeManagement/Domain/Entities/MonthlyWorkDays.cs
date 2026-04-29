namespace ThemeManagement.Domain.Entities;

public class MonthlyWorkDays
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int WorkDays { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
