using System.IO;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Desktop.Services;

public sealed class RecordingCoordinator
{
    private readonly StepFactory _stepFactory = new();
    private readonly string _sessionRoot;

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

    public event EventHandler<RecordedStep>? StepRecorded;
    public event EventHandler? SessionChanged;

    public void NewSession(string? projectName = null)
    {
        CurrentSession = new WorkflowSession
        {
            ProjectName = string.IsNullOrWhiteSpace(projectName) ? "未命名流程" : projectName
        };
        IsRecording = false;
        IsPaused = false;
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
        if (!IsRecording || IsPaused)
        {
            return false;
        }

        var step = _stepFactory.CreateStep(captureEvent);
        step.ScreenshotPath = await SaveScreenshotAsync(captureEvent, cancellationToken);
        CurrentSession.Steps.Add(step);
        StepRecorded?.Invoke(this, step);
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
