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

    [Fact]
    public void Creates_double_click_step_from_double_click_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.DoubleClick,
            Element = new ElementSnapshot
            {
                Text = "订单列表行",
                Role = "row",
                TagName = "tr"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.DoubleClick, step.Action);
        Assert.Equal("双击订单列表行", step.Title);
        Assert.Contains("订单列表行", step.SuccessCriteria);
    }

    [Fact]
    public void Creates_download_step_from_download_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.Download,
            Value = @"D:\Downloads\report.xlsx",
            PageUrl = "https://example.test/report",
            Element = new ElementSnapshot
            {
                Role = "download",
                Text = "下载完成"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.Download, step.Action);
        Assert.Equal("下载文件：report.xlsx", step.Title);
        Assert.Contains("report.xlsx", step.SuccessCriteria);
        Assert.Equal(@"D:\Downloads\report.xlsx", step.LiteralValue);
    }

    [Fact]
    public void Creates_desktop_click_step_from_desktop_click_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.DesktopClick,
            PageTitle = "另存为",
            PageUrl = "desktop://chrome",
            Element = new ElementSnapshot
            {
                Role = "desktop-control",
                Text = "保存",
                TagName = "Button"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.DesktopClick, step.Action);
        Assert.Equal("点击桌面控件：保存", step.Title);
        Assert.Contains("另存为", step.SuccessCriteria);
    }

    [Fact]
    public void Creates_desktop_double_click_step_from_desktop_double_click_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.DesktopDoubleClick,
            PageTitle = "订单管理",
            PageUrl = "desktop://erp-client",
            Element = new ElementSnapshot
            {
                Role = "desktop-control",
                Text = "订单列表行",
                TagName = "DataGridRow"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.DesktopDoubleClick, step.Action);
        Assert.Equal("双击桌面控件：订单列表行", step.Title);
        Assert.Contains("订单管理", step.SuccessCriteria);
    }

    [Fact]
    public void Creates_desktop_input_step_from_desktop_input_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.DesktopInput,
            Value = "report.xlsx",
            PageTitle = "另存为",
            PageUrl = "desktop://chrome",
            Element = new ElementSnapshot
            {
                Role = "desktop-control",
                Text = "文件名:",
                TagName = "ControlType.Edit"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.DesktopInput, step.Action);
        Assert.Equal("填写桌面控件：文件名:", step.Title);
        Assert.Equal("report.xlsx", step.LiteralValue);
    }

    [Fact]
    public void Creates_desktop_key_step_from_desktop_key_event()
    {
        var captureEvent = new CaptureEvent
        {
            EventType = CaptureEventType.DesktopKey,
            Value = "Enter",
            PageTitle = "另存为",
            PageUrl = "desktop://chrome",
            Element = new ElementSnapshot
            {
                Role = "desktop-control",
                Text = "文件名:",
                TagName = "ControlType.Edit"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.DesktopKey, step.Action);
        Assert.Equal("按下桌面快捷键：Enter", step.Title);
        Assert.Contains("另存为", step.SuccessCriteria);
    }

    [Theory]
    [InlineData(CaptureEventType.Clipboard, "Copy", "复制")]
    [InlineData(CaptureEventType.Clipboard, "Paste", "粘贴")]
    [InlineData(CaptureEventType.DesktopClipboard, "Cut", "剪切")]
    public void Creates_clipboard_step_from_clipboard_event(CaptureEventType eventType, string operation, string label)
    {
        var captureEvent = new CaptureEvent
        {
            EventType = eventType,
            Value = operation,
            PageTitle = "订单管理",
            PageUrl = "desktop://erp-client",
            Element = new ElementSnapshot
            {
                Role = "desktop-control",
                Text = "订单号",
                TagName = "ControlType.Edit"
            }
        };

        var step = _factory.CreateStep(captureEvent);

        Assert.Equal(RecordedAction.Clipboard, step.Action);
        Assert.Equal($"{label}：订单号", step.Title);
        Assert.Contains(label, step.SuccessCriteria);
    }
}
