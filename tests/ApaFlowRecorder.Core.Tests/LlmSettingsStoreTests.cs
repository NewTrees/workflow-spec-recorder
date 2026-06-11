using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public class LlmSettingsStoreTests
{
    [Fact]
    public void Saves_and_loads_api_key_as_plain_text_in_user_config_file()
    {
        var configPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "llm-settings.json");
        var store = new LlmSettingsStore(configPath);
        var settings = new LlmSettings
        {
            ProviderName = "MiniMax",
            BaseUrl = "https://api.minimaxi.com/v1",
            Model = "MiniMax-M2.7",
            ApiKey = "plain-text-key",
            Temperature = 0.2
        };

        store.Save(settings);

        var json = File.ReadAllText(configPath);
        var loaded = store.LoadOrDefault();

        Assert.Contains("plain-text-key", json);
        Assert.Equal("MiniMax", loaded.ProviderName);
        Assert.Equal("https://api.minimaxi.com/v1", loaded.BaseUrl);
        Assert.Equal("MiniMax-M2.7", loaded.Model);
        Assert.Equal("plain-text-key", loaded.ApiKey);
        Assert.Equal(0.2, loaded.Temperature);
    }

    [Fact]
    public void Missing_config_returns_domestic_default_without_api_key()
    {
        var configPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "llm-settings.json");
        var settings = new LlmSettingsStore(configPath).LoadOrDefault();

        Assert.Equal("MiniMax", settings.ProviderName);
        Assert.Equal("https://api.minimaxi.com/v1", settings.BaseUrl);
        Assert.Equal("MiniMax-M2.7", settings.Model);
        Assert.Equal(string.Empty, settings.ApiKey);
    }
}
