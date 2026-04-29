using System.Text.Json;

namespace ThemeManagement.Services;

public interface ICapacitySettings
{
    decimal Coefficient { get; set; }
}

public class CapacitySettings : ICapacitySettings
{
    private const string FileName = "capacity-settings.json";
    private readonly string _filePath;
    private decimal _coefficient = 0.9m;

    public CapacitySettings(IWebHostEnvironment env)
    {
        _filePath = Path.Combine(env.ContentRootPath, FileName);
        Load();
    }

    public decimal Coefficient
    {
        get => _coefficient;
        set
        {
            _coefficient = value;
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            var obj = JsonSerializer.Deserialize<SettingsData>(json);
            if (obj != null && obj.Coefficient > 0)
                _coefficient = obj.Coefficient;
        }
        catch { /* デフォルト値 0.9 を使用 */ }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(new SettingsData { Coefficient = _coefficient },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { /* 保存失敗は無視 */ }
    }

    private record SettingsData
    {
        public decimal Coefficient { get; init; } = 0.9m;
    }
}
