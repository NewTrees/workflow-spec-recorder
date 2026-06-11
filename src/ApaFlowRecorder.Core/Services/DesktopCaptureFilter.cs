using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public static class DesktopCaptureFilter
{
    private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox"
    };

    public static bool ShouldRecord(DesktopInteractionSnapshot snapshot, int currentProcessId)
    {
        if (snapshot.ProcessId == currentProcessId)
        {
            return false;
        }

        if (BrowserProcessNames.Contains(snapshot.ProcessName ?? string.Empty) && !LooksLikeNativeDialog(snapshot))
        {
            return false;
        }

        return true;
    }

    private static bool LooksLikeNativeDialog(DesktopInteractionSnapshot snapshot)
    {
        if (string.Equals(snapshot.WindowClassName, "#32770", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var title = snapshot.WindowTitle ?? string.Empty;
        return title.Contains("另存为", StringComparison.OrdinalIgnoreCase)
            || title.Contains("保存", StringComparison.OrdinalIgnoreCase)
            || title.Contains("打开", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Save", StringComparison.OrdinalIgnoreCase)
            || title.Contains("Open", StringComparison.OrdinalIgnoreCase);
    }
}
