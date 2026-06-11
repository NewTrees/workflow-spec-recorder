using System.Text.Encodings.Web;
using System.Text.Json;
using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class LlmSettingsStore
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public LlmSettingsStore(string? configPath = null)
    {
        _configPath = string.IsNullOrWhiteSpace(configPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ApaFlowRecorder",
                "llm-settings.json")
            : configPath;
    }

    public string ConfigPath => _configPath;

    public LlmSettings LoadOrDefault()
    {
        if (!File.Exists(_configPath))
        {
            return new LlmSettings();
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<LlmSettings>(json, _jsonOptions) ?? new LlmSettings();
        }
        catch
        {
            return new LlmSettings();
        }
    }

    public void Save(LlmSettings settings)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_configPath, JsonSerializer.Serialize(settings, _jsonOptions));
    }
}
