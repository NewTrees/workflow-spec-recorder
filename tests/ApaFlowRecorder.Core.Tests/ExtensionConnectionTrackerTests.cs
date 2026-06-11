using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

public sealed class ExtensionConnectionTrackerTests
{
    [Fact]
    public void Desktop_events_do_not_make_extension_recently_seen()
    {
        var tracker = new ExtensionConnectionTracker(TimeSpan.FromSeconds(20));
        var now = new DateTimeOffset(2026, 5, 22, 10, 40, 0, TimeSpan.Zero);

        tracker.NoteCaptureEvent(new CaptureEvent { EventType = CaptureEventType.DesktopClick }, now);

        Assert.False(tracker.IsRecentlySeen(now.AddSeconds(1)));
        Assert.Null(tracker.LastSeenAt);
    }

    [Fact]
    public void Browser_extension_events_make_extension_recently_seen()
    {
        var tracker = new ExtensionConnectionTracker(TimeSpan.FromSeconds(20));
        var now = new DateTimeOffset(2026, 5, 22, 10, 40, 0, TimeSpan.Zero);

        tracker.NoteCaptureEvent(new CaptureEvent { EventType = CaptureEventType.Click }, now);

        Assert.True(tracker.IsRecentlySeen(now.AddSeconds(1)));
        Assert.Equal(now, tracker.LastSeenAt);
    }

    [Fact]
    public void Heartbeat_expires_after_recent_window()
    {
        var tracker = new ExtensionConnectionTracker(TimeSpan.FromSeconds(20));
        var now = new DateTimeOffset(2026, 5, 22, 10, 40, 0, TimeSpan.Zero);

        tracker.NoteHeartbeat(now);

        Assert.True(tracker.IsRecentlySeen(now.AddSeconds(19)));
        Assert.False(tracker.IsRecentlySeen(now.AddSeconds(21)));
    }
}
