using ApaFlowRecorder.Core.Models;

namespace ApaFlowRecorder.Core.Services;

public sealed class StepFactory
{
    public RecordedStep CreateStep(CaptureEvent captureEvent)
    {
        return captureEvent.EventType switch
        {
            CaptureEventType.Navigation => CreateNavigationStep(captureEvent),
            CaptureEventType.Click => CreateClickStep(captureEvent),
            CaptureEventType.DoubleClick => CreateDoubleClickStep(captureEvent),
            CaptureEventType.Input => CreateFillStep(captureEvent),
            CaptureEventType.Select => CreateSelectStep(captureEvent),
            CaptureEventType.Upload => CreateUploadStep(captureEvent),
            CaptureEventType.Wait => CreateWaitStep(captureEvent),
            CaptureEventType.Download => CreateDownloadStep(captureEvent),
            CaptureEventType.DesktopClick => CreateDesktopClickStep(captureEvent),
            CaptureEventType.DesktopInput => CreateDesktopInputStep(captureEvent),
            CaptureEventType.DesktopKey => CreateDesktopKeyStep(captureEvent),
            CaptureEventType.DesktopDoubleClick => CreateDesktopDoubleClickStep(captureEvent),
            CaptureEventType.Clipboard or CaptureEventType.DesktopClipboard => CreateClipboardStep(captureEvent),
            _ => throw new ArgumentOutOfRangeException(nameof(captureEvent.EventType), captureEvent.EventType, null)
        };
    }

    private static RecordedStep CreateNavigationStep(CaptureEvent captureEvent)
    {
        var pageName = FirstNonBlank(captureEvent.PageTitle, captureEvent.PageUrl) ?? "目标页面";
        return BaseStep(captureEvent, RecordedAction.Navigate, $"打开页面：{pageName}", $"页面地址为 {captureEvent.PageUrl ?? "目标地址"}");
    }

    private static RecordedStep CreateClickStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "目标元素";
        return BaseStep(captureEvent, RecordedAction.Click, $"点击{target}", $"已触发“{target}”");
    }

    private static RecordedStep CreateDoubleClickStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "目标元素";
        return BaseStep(captureEvent, RecordedAction.DoubleClick, $"双击{target}", $"已双击“{target}”");
    }

    private static RecordedStep CreateFillStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "输入框";
        var isSensitive = string.Equals(captureEvent.Element?.InputType, "password", StringComparison.OrdinalIgnoreCase);
        var step = BaseStep(captureEvent, RecordedAction.Fill, $"填写{target}", $"“{target}”输入框已填写");
        step.IsSensitive = isSensitive;
        step.LiteralValue = isSensitive ? null : captureEvent.Value;
        step.VariableName = isSensitive ? "secret_value" : null;
        return step;
    }

    private static RecordedStep CreateSelectStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "下拉框";
        var step = BaseStep(captureEvent, RecordedAction.Select, $"选择{target}", $"“{target}”已选择“{captureEvent.Value}”");
        step.LiteralValue = captureEvent.Value;
        return step;
    }

    private static RecordedStep CreateUploadStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "文件";
        var step = BaseStep(captureEvent, RecordedAction.Upload, $"上传{target}", $"已为“{target}”选择待上传文件");
        step.VariableName = "upload_file_path";
        return step;
    }

    private static RecordedStep CreateWaitStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? captureEvent.PageTitle ?? "页面状态";
        return BaseStep(captureEvent, RecordedAction.Wait, $"等待{target}", $"“{target}”满足预期状态");
    }

    private static RecordedStep CreateDownloadStep(CaptureEvent captureEvent)
    {
        var fileName = Path.GetFileName(captureEvent.Value) ?? captureEvent.Value ?? "下载文件";
        var step = BaseStep(captureEvent, RecordedAction.Download, $"下载文件：{fileName}", $"下载任务已生成文件：{fileName}");
        step.LiteralValue = captureEvent.Value;
        step.VariableName = "download_file_path";
        return step;
    }

    private static RecordedStep CreateDesktopClickStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "桌面控件";
        return BaseStep(captureEvent, RecordedAction.DesktopClick, $"点击桌面控件：{target}", $"在窗口“{captureEvent.PageTitle ?? "桌面窗口"}”中已点击“{target}”");
    }

    private static RecordedStep CreateDesktopDoubleClickStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "桌面控件";
        return BaseStep(captureEvent, RecordedAction.DesktopDoubleClick, $"双击桌面控件：{target}", $"在窗口“{captureEvent.PageTitle ?? "桌面窗口"}”中已双击“{target}”");
    }

    private static RecordedStep CreateDesktopInputStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "桌面输入框";
        var step = BaseStep(captureEvent, RecordedAction.DesktopInput, $"填写桌面控件：{target}", $"在窗口“{captureEvent.PageTitle ?? "桌面窗口"}”中已填写“{target}”");
        step.LiteralValue = captureEvent.Value;
        return step;
    }

    private static RecordedStep CreateDesktopKeyStep(CaptureEvent captureEvent)
    {
        var keyName = string.IsNullOrWhiteSpace(captureEvent.Value) ? "快捷键" : captureEvent.Value;
        return BaseStep(captureEvent, RecordedAction.DesktopKey, $"按下桌面快捷键：{keyName}", $"在窗口“{captureEvent.PageTitle ?? "桌面窗口"}”中已按下 {keyName}");
    }

    private static RecordedStep CreateClipboardStep(CaptureEvent captureEvent)
    {
        var target = captureEvent.Element?.DisplayName ?? "当前控件";
        var label = ClipboardOperationLabel(captureEvent.Value);
        return BaseStep(captureEvent, RecordedAction.Clipboard, $"{label}：{target}", $"已对“{target}”执行{label}操作");
    }

    private static string ClipboardOperationLabel(string? operation)
    {
        return operation switch
        {
            "Copy" => "复制",
            "Paste" => "粘贴",
            "Cut" => "剪切",
            _ => "剪贴板"
        };
    }

    private static RecordedStep BaseStep(CaptureEvent captureEvent, RecordedAction action, string title, string successCriteria)
    {
        return new RecordedStep
        {
            Action = action,
            CapturedAtUtc = captureEvent.CapturedAtUtc,
            PageUrl = captureEvent.PageUrl,
            PageTitle = captureEvent.PageTitle,
            Element = captureEvent.Element,
            Title = title,
            SuccessCriteria = successCriteria
        };
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
