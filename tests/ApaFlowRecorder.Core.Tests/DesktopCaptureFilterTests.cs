using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Core.Tests;

public class DesktopCaptureFilterTests
{
    [Fact]
    public void ShouldRecord_rejects_recorder_own_process()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 100,
            ProcessName = "ApaFlowRecorder.Desktop",
            WindowTitle = "Workflow Spec Recorder",
            WindowClassName = "HwndWrapper"
        };

        Assert.False(DesktopCaptureFilter.ShouldRecord(snapshot, currentProcessId: 100));
    }

    [Fact]
    public void ShouldRecord_rejects_regular_browser_page_clicks_to_avoid_duplicate_dom_steps()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 200,
            ProcessName = "chrome",
            WindowTitle = "懂车帝 - Google Chrome",
            WindowClassName = "Chrome_WidgetWin_1",
            ControlClassName = "Chrome_RenderWidgetHostHWND"
        };

        Assert.False(DesktopCaptureFilter.ShouldRecord(snapshot, currentProcessId: 100));
    }

    [Fact]
    public void ShouldRecord_accepts_browser_file_save_dialogs()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 200,
            ProcessName = "chrome",
            WindowTitle = "另存为",
            WindowClassName = "#32770",
            ControlName = "保存",
            ControlClassName = "Button"
        };

        Assert.True(DesktopCaptureFilter.ShouldRecord(snapshot, currentProcessId: 100));
    }

    [Fact]
    public void Mapper_creates_desktop_click_capture_event_with_window_and_control_context()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 300,
            ProcessName = "erp-client",
            WindowTitle = "订单管理",
            WindowClassName = "MainWindow",
            ControlName = "查询",
            ControlClassName = "Button"
        };

        var captureEvent = DesktopCaptureMapper.CreateClickEvent(snapshot);

        Assert.Equal(CaptureEventType.DesktopClick, captureEvent.EventType);
        Assert.Equal("desktop://erp-client", captureEvent.PageUrl);
        Assert.Equal("订单管理", captureEvent.PageTitle);
        Assert.Equal("查询", captureEvent.Element?.Text);
        Assert.Contains("window=订单管理", captureEvent.Element?.CssSelector);
    }

    [Fact]
    public void Mapper_copies_desktop_screenshot_data_url_to_capture_event()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 300,
            ProcessName = "erp-client",
            WindowTitle = "订单管理",
            ControlName = "查询",
            ControlClassName = "Button",
            ScreenshotDataUrl = "data:image/png;base64,AAAA"
        };

        var captureEvent = DesktopCaptureMapper.CreateClickEvent(snapshot);

        Assert.Equal("data:image/png;base64,AAAA", captureEvent.ScreenshotDataUrl);
    }

    [Fact]
    public void Mapper_creates_desktop_double_click_capture_event()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 300,
            ProcessName = "erp-client",
            WindowTitle = "订单管理",
            ControlName = "订单列表行",
            ControlClassName = "DataGridRow"
        };

        var captureEvent = DesktopCaptureMapper.CreateDoubleClickEvent(snapshot);

        Assert.Equal(CaptureEventType.DesktopDoubleClick, captureEvent.EventType);
        Assert.Equal("订单列表行", captureEvent.Element?.Text);
    }

    [Fact]
    public void Mapper_creates_desktop_clipboard_capture_event_for_copy_or_paste_shortcuts()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 300,
            ProcessName = "erp-client",
            WindowTitle = "订单管理",
            ControlName = "订单号",
            ControlClassName = "ControlType.Edit"
        };

        var captureEvent = DesktopCaptureMapper.CreateClipboardEvent(snapshot, "Paste");

        Assert.Equal(CaptureEventType.DesktopClipboard, captureEvent.EventType);
        Assert.Equal("Paste", captureEvent.Value);
        Assert.Equal("订单号", captureEvent.Element?.Text);
    }

    [Fact]
    public void Mapper_creates_desktop_input_capture_event_without_losing_control_context()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 200,
            ProcessName = "chrome",
            WindowTitle = "另存为",
            WindowClassName = "#32770",
            ControlName = "文件名:",
            ControlClassName = "ControlType.Edit"
        };

        var captureEvent = DesktopCaptureMapper.CreateInputEvent(snapshot, "report.xlsx");

        Assert.Equal(CaptureEventType.DesktopInput, captureEvent.EventType);
        Assert.Equal("report.xlsx", captureEvent.Value);
        Assert.Equal("文件名:", captureEvent.Element?.Text);
        Assert.Contains("control=文件名:", captureEvent.Element?.CssSelector);
    }

    [Fact]
    public void Mapper_creates_desktop_key_capture_event_for_confirm_keys()
    {
        var snapshot = new DesktopInteractionSnapshot
        {
            ProcessId = 200,
            ProcessName = "chrome",
            WindowTitle = "另存为",
            WindowClassName = "#32770",
            ControlName = "文件名:",
            ControlClassName = "ControlType.Edit"
        };

        var captureEvent = DesktopCaptureMapper.CreateKeyEvent(snapshot, "Enter");

        Assert.Equal(CaptureEventType.DesktopKey, captureEvent.EventType);
        Assert.Equal("Enter", captureEvent.Value);
        Assert.Equal("文件名:", captureEvent.Element?.Text);
    }
}
