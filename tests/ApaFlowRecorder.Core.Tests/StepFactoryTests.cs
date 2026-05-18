using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public class StepFactoryTests
{
    private readonly StepFactory _factory = new();

    [Fact]
    public void Creates_navigation_step_from_navigation_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.Navigation,
            PageTitle = "报销系统",
            PageUrl = "https://example.test/login"
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.Navigate, step.Action);
        Assert.Equal("打开页面：报销系统", step.Title);
        Assert.Contains("https://example.test/login", step.SuccessCriteria);
    }

    [Fact]
    public void Creates_fill_step_from_text_input_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.Input,
            Value = "张三",
            Element = new ElementSnapshot
            {
                Label = "申请人",
                Role = "textbox",
                TagName = "input"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.Fill, step.Action);
        Assert.Equal("填写申请人", step.Title);
        Assert.Equal("张三", step.LiteralValue);
        Assert.False(step.IsSensitive);
    }

    [Fact]
    public void Password_input_is_masked_and_suggests_variable()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.Input,
            Value = "super-secret",
            Element = new ElementSnapshot
            {
                Label = "密码",
                Role = "textbox",
                TagName = "input",
                InputType = "password"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.Fill, step.Action);
        Assert.Null(step.LiteralValue);
        Assert.True(step.IsSensitive);
        Assert.Equal("secret_value", step.VariableName);
    }

    [Fact]
    public void Creates_select_step_from_select_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.Select,
            Value = "电子发票",
            Element = new ElementSnapshot
            {
                Label = "发票类型",
                Role = "combobox",
                TagName = "select"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.Select, step.Action);
        Assert.Equal("选择发票类型", step.Title);
        Assert.Equal("电子发票", step.LiteralValue);
    }

    [Fact]
    public void Upload_event_uses_file_variable_placeholder()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.Upload,
            Value = "invoice.pdf",
            Element = new ElementSnapshot
            {
                Label = "发票",
                Role = "textbox",
                TagName = "input",
                InputType = "file"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.Upload, step.Action);
        Assert.Equal("上传发票", step.Title);
        Assert.Null(step.LiteralValue);
        Assert.Equal("upload_file_path", step.VariableName);
    }

    [Fact]
    public void Creates_click_step_from_click_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.Click,
            Element = new ElementSnapshot
            {
                Text = "立即登录",
                Role = "button",
                TagName = "button"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.Click, step.Action);
        Assert.Equal("点击立即登录", step.Title);
        Assert.Contains("立即登录", step.SuccessCriteria);
    }
}

