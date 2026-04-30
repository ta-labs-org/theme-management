namespace ThemeManagement.Domain.Entities;

public class NotificationSetting
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>通知頻度: "Realtime" | "Daily" | "Weekly"</summary>
    public string Frequency { get; set; } = "Daily";

    public bool IsActive { get; set; } = true;
    public DateTime? LastNotifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
