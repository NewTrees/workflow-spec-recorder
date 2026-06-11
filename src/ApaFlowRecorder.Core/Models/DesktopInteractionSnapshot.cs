namespace ApaFlowRecorder.Core.Models;

public sealed class DesktopInteractionSnapshot
{
    public int ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }
    public string? WindowClassName { get; set; }
    public string? ControlName { get; set; }
    public string? ControlClassName { get; set; }
    public string? ScreenshotDataUrl { get; set; }
}
