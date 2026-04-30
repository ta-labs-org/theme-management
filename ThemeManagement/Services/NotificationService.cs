using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

public interface INotificationService
{
    Task<List<NotificationSetting>> GetAllAsync();
    Task<NotificationSetting?> GetByIdAsync(int id);
    Task SaveAsync(NotificationSetting setting);
    Task DeleteAsync(int id);
    Task SendAlertEmailsAsync();
    Task TrySendRealtimeAlertsAsync();
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext db,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<NotificationSetting>> GetAllAsync()
        => await _db.NotificationSettings.OrderBy(n => n.Email).ToListAsync();

    public async Task<NotificationSetting?> GetByIdAsync(int id)
        => await _db.NotificationSettings.FindAsync(id);

    public async Task SaveAsync(NotificationSetting setting)
    {
        if (setting.Id == 0)
            _db.NotificationSettings.Add(setting);
        else
            _db.NotificationSettings.Update(setting);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.NotificationSettings.FindAsync(id);
        if (entity != null)
        {
            _db.NotificationSettings.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>リアルタイム通知対象の受信者にのみメールを送信します。</summary>
    public async Task TrySendRealtimeAlertsAsync()
    {
        var hasRealtime = await _db.NotificationSettings
            .AnyAsync(n => n.IsActive && n.Frequency == "Realtime");
        if (!hasRealtime) return;

        await SendAlertEmailsAsync(frequencyFilter: "Realtime");
    }

    /// <summary>全アクティブ受信者（頻度フィルタなし）にアラートメールを送信します。</summary>
    public Task SendAlertEmailsAsync() => SendAlertEmailsAsync(frequencyFilter: null);

    private async Task SendAlertEmailsAsync(string? frequencyFilter)
    {
        var query = _db.NotificationSettings.Where(n => n.IsActive);
        if (frequencyFilter != null)
            query = query.Where(n => n.Frequency == frequencyFilter);

        var recipients = await query.ToListAsync();
        if (recipients.Count == 0) return;

        var (engineerAlerts, themeAlerts) = await CollectAlertsAsync();

        if (engineerAlerts.Count == 0 && themeAlerts.Count == 0) return;

        var subject = "【テーマ管理】アラート通知";
        var body = BuildEmailBody(engineerAlerts, themeAlerts);

        foreach (var recipient in recipients)
        {
            try
            {
                await SendEmailAsync(recipient.Email, subject, body);
                recipient.LastNotifiedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "メール送信に失敗しました: {Email}", recipient.Email);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task<(List<string> engineerAlerts, List<string> themeAlerts)> CollectAlertsAsync()
    {
        var now = DateTime.Now;
        var year = now.Year;
        var month = now.Month;

        // エンジニア稼働率超過アラート
        var engineers = await _db.Engineers
            .Include(e => e.Grade)
            .Where(e => e.IsActive)
            .ToListAsync();

        var engineerIds = engineers.Select(e => e.Id).ToList();

        var allocations = await _db.EngineerThemeAllocations
            .Where(a => engineerIds.Contains(a.EngineerId) && a.Year == year && a.Month == month)
            .ToListAsync();

        var monthlyWorkDays = await _db.MonthlyWorkDays
            .FirstOrDefaultAsync(m => m.Year == year && m.Month == month);

        var adjustments = await _db.EngineerMonthlyAdjustments
            .Where(a => engineerIds.Contains(a.EngineerId) && a.Year == year && a.Month == month)
            .ToListAsync();

        var engineerAlerts = new List<string>();
        foreach (var eng in engineers)
        {
            var adj = adjustments.FirstOrDefault(a => a.EngineerId == eng.Id);
            int workDays = adj?.WorkDays ?? monthlyWorkDays?.WorkDays ?? 0;
            var maxHours = workDays * 8m;
            var totalHours = allocations.Where(a => a.EngineerId == eng.Id).Sum(a => a.AllocatedHours);
            if (maxHours > 0 && totalHours > maxHours)
            {
                var rate = totalHours / maxHours * 100m;
                engineerAlerts.Add($"{eng.Name}：稼働率 {rate:F1}%（割り当て {totalHours:F1}h / 上限 {maxHours:F1}h）");
            }
        }

        // テーマ消化率超過・期限超過アラート
        var themes = await _db.Themes
            .Where(t => t.Status == "Active")
            .ToListAsync();

        var themeIds = themes.Select(t => t.Id).ToList();

        var themeAllocs = await _db.EngineerThemeAllocations
            .Include(a => a.Engineer).ThenInclude(e => e.Grade)
            .Where(a => themeIds.Contains(a.ThemeId))
            .ToListAsync();

        var today = DateOnly.FromDateTime(now);
        var themeAlerts = new List<string>();
        foreach (var theme in themes)
        {
            var tAllocs = themeAllocs.Where(a => a.ThemeId == theme.Id).ToList();
            bool useCost = theme.OrderType == "社用開発";
            var allocCost = tAllocs.Sum(a =>
                a.AllocatedHours * (useCost ? a.Engineer.Grade.UnitCostPrice : a.Engineer.Grade.UnitSalePrice));
            var progressRate = theme.OrderAmount > 0 ? allocCost / theme.OrderAmount * 100m : 0m;

            bool overBudget = progressRate > 100m;
            bool pastDeadline = theme.ActualCompletionDate == null && theme.EstimatedCompletionDate < today;

            if (overBudget)
                themeAlerts.Add($"{theme.Name}：消化率 {progressRate:F1}%（受注金額超過）");
            else if (pastDeadline)
                themeAlerts.Add($"{theme.Name}：完了予定 {theme.EstimatedCompletionDate:yyyy/MM/dd}（期限超過）");
        }

        return (engineerAlerts, themeAlerts);
    }

    private static string BuildEmailBody(List<string> engineerAlerts, List<string> themeAlerts)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("テーマ管理システムからのアラート通知です。");
        sb.AppendLine();

        if (engineerAlerts.Count > 0)
        {
            sb.AppendLine("■ エンジニア稼働率超過");
            foreach (var alert in engineerAlerts)
                sb.AppendLine($"  ・{alert}");
            sb.AppendLine();
        }

        if (themeAlerts.Count > 0)
        {
            sb.AppendLine("■ テーマ・案件アラート");
            foreach (var alert in themeAlerts)
                sb.AppendLine($"  ・{alert}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("テーマ管理システム 自動通知");
        return sb.ToString();
    }

    private async Task SendEmailAsync(string toAddress, string subject, string body)
    {
        var smtpSection = _configuration.GetSection("Smtp");
        var host = smtpSection["Host"] ?? "localhost";
        var port = int.TryParse(smtpSection["Port"], out var p) ? p : 25;
        var enableSsl = bool.TryParse(smtpSection["EnableSsl"], out var ssl) && ssl;
        var username = smtpSection["Username"] ?? string.Empty;
        var password = smtpSection["Password"] ?? string.Empty;
        var fromAddress = smtpSection["FromAddress"] ?? "noreply@theme-management.local";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        using var message = new MailMessage(fromAddress, toAddress, subject, body)
        {
            IsBodyHtml = false
        };

        await client.SendMailAsync(message);
        _logger.LogInformation("アラートメールを送信しました: {Email}", toAddress);
    }
}
