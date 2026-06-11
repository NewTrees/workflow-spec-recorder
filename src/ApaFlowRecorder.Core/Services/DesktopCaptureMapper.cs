using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public static class DesktopCaptureMapper
{
    public static CaptureEvent CreateClickEvent(DesktopInteractionSnapshot snapshot)
    {
        return new CaptureEvent
        {
            EventType = CaptureEventType.DesktopClick,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            PageTitle = snapshot.WindowTitle,
            PageUrl = string.IsNullOrWhiteSpace(snapshot.ProcessName) ? "desktop://unknown" : $"desktop://{snapshot.ProcessName}",
            ScreenshotDataUrl = snapshot.ScreenshotDataUrl,
            Element = new ElementSnapshot
            {
                Role = "desktop-control",
                Name = snapshot.WindowTitle,
                Text = FirstNonBlank(snapshot.ControlName, snapshot.WindowTitle, snapshot.ControlClassName),
                TagName = snapshot.ControlClassName,
                CssSelector = Describe(snapshot)
            }
        };
    }

    public static CaptureEvent CreateDoubleClickEvent(DesktopInteractionSnapshot snapshot)
    {
        var captureEvent = CreateBaseEvent(snapshot, CaptureEventType.DesktopDoubleClick);
        return captureEvent;
    }

    public static CaptureEvent CreateInputEvent(DesktopInteractionSnapshot snapshot, string value)
    {
        var captureEvent = CreateBaseEvent(snapshot, CaptureEventType.DesktopInput);
        captureEvent.Value = value;
        return captureEvent;
    }

    public static CaptureEvent CreateKeyEvent(DesktopInteractionSnapshot snapshot, string keyName)
    {
        var captureEvent = CreateBaseEvent(snapshot, CaptureEventType.DesktopKey);
        captureEvent.Value = keyName;
        return captureEvent;
    }

    public static CaptureEvent CreateClipboardEvent(DesktopInteractionSnapshot snapshot, string operation)
    {
        var captureEvent = CreateBaseEvent(snapshot, CaptureEventType.DesktopClipboard);
        captureEvent.Value = operation;
        return captureEvent;
    }

    private static CaptureEvent CreateBaseEvent(DesktopInteractionSnapshot snapshot, CaptureEventType eventType)
    {
        return new CaptureEvent
        {
            EventType = eventType,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            PageTitle = snapshot.WindowTitle,
            PageUrl = string.IsNullOrWhiteSpace(snapshot.ProcessName) ? "desktop://unknown" : $"desktop://{snapshot.ProcessName}",
            ScreenshotDataUrl = snapshot.ScreenshotDataUrl,
            Element = new ElementSnapshot
            {
                Role = "desktop-control",
                Name = snapshot.WindowTitle,
                Text = FirstNonBlank(snapshot.ControlName, snapshot.WindowTitle, snapshot.ControlClassName),
                TagName = snapshot.ControlClassName,
                CssSelector = Describe(snapshot)
            }
        };
    }

    public static string Describe(DesktopInteractionSnapshot snapshot)
    {
        var parts = new[]
        {
            $"process={snapshot.ProcessName}",
            $"window={snapshot.WindowTitle}",
            $"windowClass={snapshot.WindowClassName}",
            $"control={snapshot.ControlName}",
            $"controlClass={snapshot.ControlClassName}"
        };
        return string.Join("; ", parts.Where(part => !part.EndsWith("=", StringComparison.Ordinal)));
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
