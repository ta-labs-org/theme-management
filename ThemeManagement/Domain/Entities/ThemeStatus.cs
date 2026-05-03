namespace ThemeManagement.Domain.Entities;

public static class ThemeStatus
{
    public const string Quoting = "Quoting";
    public const string Confirmed = "Confirmed";
    public const string Active = "Active";
    public const string OnHold = "OnHold";
    public const string Completed = "Completed";
    public const string Lost = "Lost";
    public const string Cancelled = "Cancelled";

    /// <summary>
    /// 「アクティブ」とみなすステータスの集合（見積中・受注確定・進行中・一時停止）
    /// </summary>
    public static readonly HashSet<string> ActiveStatuses =
        [Quoting, Confirmed, Active, OnHold];
}
