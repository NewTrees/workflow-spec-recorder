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
            CaptureEventType.Input => CreateFillStep(captureEvent),
            CaptureEventType.Select => CreateSelectStep(captureEvent),
            CaptureEventType.Upload => CreateUploadStep(captureEvent),
            CaptureEventType.Wait => CreateWaitStep(captureEvent),
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

