using Microsoft.JSInterop;
using System.Text.Json;

namespace ThemeManagement.Services;

public class WidgetConfig
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsVisible { get; set; } = true;
    public int Order { get; set; }
}

public class DashboardSettings
{
    public List<WidgetConfig> Widgets { get; set; } = [];
    public List<string> PinnedKpis { get; set; } = ["project-count", "work-rate", "monthly-cost"];
}

public interface IDashboardSettingsService
{
    Task<DashboardSettings> LoadAsync();
    Task SaveAsync(DashboardSettings settings);
}

public class DashboardSettingsService : IDashboardSettingsService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "dashboard-settings";

    public static readonly List<WidgetConfig> DefaultWidgets =
    [
        new() { Id = "kpi", DisplayName = "KPIカード", IsVisible = true, Order = 0 },
        new() { Id = "engineer-summary", DisplayName = "エンジニア稼働サマリ", IsVisible = true, Order = 1 },
        new() { Id = "theme-progress", DisplayName = "テーマ進捗サマリ", IsVisible = true, Order = 2 }
    ];

    public static readonly List<string> DefaultPinnedKpis = ["project-count", "work-rate", "monthly-cost"];

    public static DashboardSettings CreateDefaultSettings() => new()
    {
        Widgets = DefaultWidgets.Select(w => new WidgetConfig
        {
            Id = w.Id,
            DisplayName = w.DisplayName,
            IsVisible = w.IsVisible,
            Order = w.Order
        }).ToList(),
        PinnedKpis = [.. DefaultPinnedKpis]
    };

    public DashboardSettingsService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<DashboardSettings> LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                var settings = JsonSerializer.Deserialize<DashboardSettings>(json);
                if (settings != null)
                {
                    // Merge with defaults to handle any new widgets added in future updates
                    foreach (var def in DefaultWidgets)
                    {
                        var existing = settings.Widgets.FirstOrDefault(w => w.Id == def.Id);
                        if (existing == null)
                        {
                            settings.Widgets.Add(new WidgetConfig
                            {
                                Id = def.Id,
                                DisplayName = def.DisplayName,
                                IsVisible = def.IsVisible,
                                Order = settings.Widgets.Count
                            });
                        }
                        else
                        {
                            existing.DisplayName = def.DisplayName;
                        }
                    }
                    return settings;
                }
            }
        }
        catch
        {
            // LocalStorage access can fail (e.g. SSR, private browsing restrictions, malformed JSON).
            // Silently fall back to defaults so the dashboard always renders correctly.
        }

        return CreateDefaultSettings();
    }

    public async Task SaveAsync(DashboardSettings settings)
    {
        var json = JsonSerializer.Serialize(settings);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
