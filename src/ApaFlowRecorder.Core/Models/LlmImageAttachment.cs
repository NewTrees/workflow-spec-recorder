namespace ApaFlowRecorder.Core.Models;

public sealed class LlmImageAttachment
{
    public string Path { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
}
