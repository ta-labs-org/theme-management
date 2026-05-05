using System.Text.Json;

namespace ThemeManagement.Services;

public interface ICopilotAgentSettings
{
    string Model { get; set; }
    string[] AvailableModels { get; }
}

public class CopilotAgentSettings : ICopilotAgentSettings
{
    private const string FileName = "copilot-agent-settings.json";
    private readonly string _filePath;
    private string _model = "claude-sonnet-4.6";

    public string[] AvailableModels { get; }

    public CopilotAgentSettings(IWebHostEnvironment env, IConfiguration configuration)
    {
        _filePath = Path.Combine(env.ContentRootPath, FileName);
        // appsettings.json の値をデフォルトとして使う
        _model = configuration["CopilotAgent:Model"] ?? _model;
        AvailableModels = configuration.GetSection("CopilotAgent:AvailableModels").Get<string[]>()
            ?? ["claude-sonnet-4.6", "claude-sonnet-4-5", "gpt-4.1", "gpt-4o", "o3-mini"];
        Load();
    }

    public string Model
    {
        get => _model;
        set
        {
            _model = value;
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
            if (obj != null && !string.IsNullOrWhiteSpace(obj.Model))
                _model = obj.Model;
        }
        catch { /* デフォルト値を使用 */ }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(new SettingsData { Model = _model },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { /* 保存失敗は無視 */ }
    }

    private record SettingsData
    {
        public string Model { get; init; } = "claude-sonnet-4.6";
    }
}
