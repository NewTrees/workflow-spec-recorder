namespace ApaFlowRecorder.Core.Models;

public sealed class LlmSettings
{
    public string ProviderName { get; set; } = "MiniMax";
    public string BaseUrl { get; set; } = "https://api.minimaxi.com/v1";
    public string Model { get; set; } = "MiniMax-M2.7";
    public string ApiKey { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.1;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Model) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}
