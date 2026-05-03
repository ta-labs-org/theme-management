using Microsoft.EntityFrameworkCore;
using ThemeManagement.Data;
using ThemeManagement.Domain.Entities;

namespace ThemeManagement.Services;

/// <summary>
/// 定期的にアラート状況をチェックし、通知頻度設定に応じてメール通知を送信するバックグラウンドサービス。
/// </summary>
public class AlertCheckBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertCheckBackgroundService> _logger;

    // チェック間隔（1時間ごとに実行）
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public AlertCheckBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AlertCheckBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AlertCheckBackgroundService が起動しました");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "アラートチェック中にエラーが発生しました");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunCheckAsync()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.Now;

        var recipients = await db.NotificationSettings
            .Where(n => n.IsActive && n.Frequency != "Realtime")
            .ToListAsync();

        if (recipients.Count == 0) return;

        bool shouldSend = recipients.Any(r => r.Frequency switch
        {
            "Daily" => r.LastNotifiedAt == null || r.LastNotifiedAt.Value.Date < now.Date,
            "Weekly" => r.LastNotifiedAt == null || (now - r.LastNotifiedAt.Value).TotalDays >= 7,
            _ => false
        });

        if (!shouldSend) return;

        _logger.LogInformation("アラートチェックを実行します: {Time}", now);
        await notificationService.SendAlertEmailsAsync();
    }
}
