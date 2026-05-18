namespace ApaFlowRecorder.Core.Models;

public sealed class CaptureEvent
{
    public CaptureEventType EventType { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? PageUrl { get; set; }
    public string? PageTitle { get; set; }
    public string? Value { get; set; }
    public string? ScreenshotDataUrl { get; set; }
    public ElementSnapshot? Element { get; set; }
}

