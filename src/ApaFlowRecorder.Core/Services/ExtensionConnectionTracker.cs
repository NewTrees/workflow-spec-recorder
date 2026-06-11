using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class ExtensionConnectionTracker
{
    private readonly TimeSpan _recentWindow;

    public ExtensionConnectionTracker(TimeSpan? recentWindow = null)
    {
        _recentWindow = recentWindow ?? TimeSpan.FromSeconds(20);
    }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public void NoteHeartbeat(DateTimeOffset? now = null)
    {
        LastSeenAt = now ?? DateTimeOffset.Now;
    }

    public void NoteCaptureEvent(CaptureEvent captureEvent, DateTimeOffset? now = null)
    {
        if (!IsBrowserExtensionEvent(captureEvent.EventType))
        {
            return;
        }

        NoteHeartbeat(now);
    }

    public bool IsRecentlySeen(DateTimeOffset? now = null)
    {
        if (LastSeenAt is null)
        {
            return false;
        }

        return (now ?? DateTimeOffset.Now) - LastSeenAt.Value < _recentWindow;
    }

    public static bool IsBrowserExtensionEvent(CaptureEventType eventType)
    {
        return eventType is not CaptureEventType.DesktopClick
            and not CaptureEventType.DesktopInput
            and not CaptureEventType.DesktopKey
            and not CaptureEventType.DesktopDoubleClick
            and not CaptureEventType.DesktopClipboard;
    }
}
