using System.IO;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Desktop.Services;

public sealed class RecordingCoordinator
{
    private readonly StepFactory _stepFactory = new();
    private readonly string _sessionRoot;
    private static readonly TimeSpan ClickNavigationMergeWindow = TimeSpan.FromSeconds(2);
    private int _receivedEventCount;
    private int _acceptedEventCount;
    private int _ignoredEventCount;

    public RecordingCoordinator()
    {
        _sessionRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ApaFlowRecorder",
            "sessions");
    }

    public WorkflowSession CurrentSession { get; private set; } = new();
    public bool IsRecording { get; private set; }
    public bool IsPaused { get; private set; }
    public int ReceivedEventCount => _receivedEventCount;
    public int AcceptedEventCount => _acceptedEventCount;
    public int IgnoredEventCount => _ignoredEventCount;

    public event EventHandler<RecordedStep>? StepRecorded;
    public event EventHandler? SessionChanged;
    public event EventHandler<CaptureEventReport>? CaptureEventReceived;

    public void NewSession(string? projectName = null)
    {
        CurrentSession = new WorkflowSession
        {
            ProjectName = string.IsNullOrWhiteSpace(projectName) ? "未命名流程" : projectName
        };
        IsRecording = false;
        IsPaused = false;
        _receivedEventCount = 0;
        _acceptedEventCount = 0;
        _ignoredEventCount = 0;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        IsRecording = true;
        IsPaused = false;
    }

    public void Pause()
    {
        if (IsRecording)
        {
            IsPaused = true;
        }
    }

    public void Stop()
    {
        IsRecording = false;
        IsPaused = false;
    }

    public async Task<bool> HandleCaptureEventAsync(CaptureEvent captureEvent, CancellationToken cancellationToken = default)
    {
        _receivedEventCount++;

        if (!IsRecording || IsPaused)
        {
            _ignoredEventCount++;
            CaptureEventReceived?.Invoke(
                this,
                new CaptureEventReport(captureEvent, false, IsPaused ? "录制已暂停" : "尚未开始录制"));
            return false;
        }

        if (TryMergeNavigationAfterClick(captureEvent))
        {
            _acceptedEventCount++;
            CaptureEventReceived?.Invoke(this, new CaptureEventReport(captureEvent, true, "导航已合并到上一条点击步骤"));
            SessionChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        var step = _stepFactory.CreateStep(captureEvent);
        step.ScreenshotPath = await SaveScreenshotAsync(captureEvent, cancellationToken);
        CurrentSession.Steps.Add(step);
        _acceptedEventCount++;
        CaptureEventReceived?.Invoke(this, new CaptureEventReport(captureEvent, true, "已记录为步骤"));
        StepRecorded?.Invoke(this, step);
        return true;
    }

    private bool TryMergeNavigationAfterClick(CaptureEvent captureEvent)
    {
        if (captureEvent.EventType != CaptureEventType.Navigation || CurrentSession.Steps.Count == 0)
        {
            return false;
        }

        var previousStep = CurrentSession.Steps[^1];
        if (previousStep.Action is not (RecordedAction.Click or RecordedAction.DoubleClick))
        {
            return false;
        }

        var elapsed = captureEvent.CapturedAtUtc - previousStep.CapturedAtUtc;
        if (elapsed < TimeSpan.Zero || elapsed > ClickNavigationMergeWindow)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(captureEvent.PageTitle))
        {
            previousStep.PageTitle = captureEvent.PageTitle;
        }

        if (!string.IsNullOrWhiteSpace(captureEvent.PageUrl))
        {
            previousStep.PageUrl = captureEvent.PageUrl;
        }

        if (!string.IsNullOrWhiteSpace(captureEvent.PageTitle))
        {
            previousStep.SuccessCriteria = $"已进入“{captureEvent.PageTitle}”页面";
        }
        else if (!string.IsNullOrWhiteSpace(captureEvent.PageUrl))
        {
            previousStep.SuccessCriteria = "已进入点击后的目标页面";
        }

        return true;
    }

    private async Task<string?> SaveScreenshotAsync(CaptureEvent captureEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(captureEvent.ScreenshotDataUrl))
        {
            return null;
        }

        var commaIndex = captureEvent.ScreenshotDataUrl.IndexOf(',');
        if (commaIndex < 0)
        {
            return null;
        }

        var base64Payload = captureEvent.ScreenshotDataUrl[(commaIndex + 1)..];
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64Payload);
        }
        catch (FormatException)
        {
            return null;
        }

        var screenshotDirectory = Path.Combine(_sessionRoot, CurrentSession.Id.ToString("N"), "screenshots");
        Directory.CreateDirectory(screenshotDirectory);
        var path = Path.Combine(screenshotDirectory, $"{captureEvent.CapturedAtUtc:yyyyMMdd-HHmmssfff}.png");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return path;
    }
}

public sealed record CaptureEventReport(CaptureEvent CaptureEvent, bool Accepted, string Reason);
