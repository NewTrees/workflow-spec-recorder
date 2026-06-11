using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Forms;
using ApaFlowRecorder.Core.Models;
using ApaFlowRecorder.Core.Services;

namespace ApaFlowRecorder.Desktop.Services;

public sealed class DesktopInteractionCaptureService : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WhKeyboardLl = 13;
    private const int WmLButtonDown = 0x0201;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkBack = 0x08;
    private const int VkTab = 0x09;
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;
    private const int VkC = 0x43;
    private const int VkV = 0x56;
    private const int VkX = 0x58;
    private const int VkControl = 0x11;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int SmCxDoubleClk = 36;
    private const int SmCyDoubleClk = 37;
    private const uint GaRoot = 2;
    private readonly Func<CaptureEvent, CancellationToken, Task<bool>> _handleCaptureEventAsync;
    private readonly int _currentProcessId = Environment.ProcessId;
    private readonly LowLevelHookProc _mouseProc;
    private readonly LowLevelHookProc _keyboardProc;
    private readonly object _clickLock = new();
    private readonly StringBuilder _inputBuffer = new();
    private DesktopInteractionSnapshot? _inputSnapshot;
    private string? _inputDescriptor;
    private DesktopInteractionSnapshot? _pendingClickSnapshot;
    private Point _pendingClickPoint;
    private System.Threading.Timer? _pendingClickTimer;
    private IntPtr _mouseHook;
    private IntPtr _keyboardHook;

    public DesktopInteractionCaptureService(Func<CaptureEvent, CancellationToken, Task<bool>> handleCaptureEventAsync)
    {
        _handleCaptureEventAsync = handleCaptureEventAsync;
        _mouseProc = HandleMouseEvent;
        _keyboardProc = HandleKeyboardEvent;
    }

    public bool IsRunning => _mouseHook != IntPtr.Zero || _keyboardHook != IntPtr.Zero;

    public void Start()
    {
        if (_mouseHook != IntPtr.Zero && _keyboardHook != IntPtr.Zero)
        {
            return;
        }

        if (_mouseHook == IntPtr.Zero)
        {
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, IntPtr.Zero, 0);
        }

        if (_keyboardHook == IntPtr.Zero)
        {
            _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, IntPtr.Zero, 0);
        }
    }

    public void Stop()
    {
        FlushInputBuffer();
        FlushPendingClick();
        if (!IsRunning)
        {
            return;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr HandleMouseEvent(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == WmLButtonDown)
        {
            var mouse = Marshal.PtrToStructure<MouseHookStruct>(lParam);
            FlushInputBuffer();
            TryRecordClickAtCursor(mouse.Point);
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr HandleKeyboardEvent(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == WmKeyDown || wParam == WmSysKeyDown))
        {
            var keyboard = Marshal.PtrToStructure<KeyboardHookStruct>(lParam);
            TryRecordKeyboard(keyboard.VirtualKeyCode);
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void TryRecordClickAtCursor(Point point)
    {
        try
        {
            var snapshot = CaptureAtPoint(point);
            if (!DesktopCaptureFilter.ShouldRecord(snapshot, _currentProcessId))
            {
                return;
            }

            snapshot.ScreenshotDataUrl = CaptureDesktopScreenshotDataUrl();
            QueueClick(snapshot, point);
        }
        catch
        {
            // Desktop capture must never block or crash the user's workflow.
        }
    }

    private void TryRecordKeyboard(int virtualKeyCode)
    {
        try
        {
            var snapshot = CaptureFocusedElement();
            if (!DesktopCaptureFilter.ShouldRecord(snapshot, _currentProcessId))
            {
                return;
            }

            if (IsConfirmKey(virtualKeyCode, out var keyName))
            {
                FlushInputBuffer();
                snapshot.ScreenshotDataUrl = CaptureDesktopScreenshotDataUrl();
                _ = _handleCaptureEventAsync(DesktopCaptureMapper.CreateKeyEvent(snapshot, keyName), CancellationToken.None);
                return;
            }

            if (IsClipboardShortcut(virtualKeyCode, out var clipboardOperation))
            {
                FlushInputBuffer();
                snapshot.ScreenshotDataUrl = CaptureDesktopScreenshotDataUrl();
                _ = _handleCaptureEventAsync(DesktopCaptureMapper.CreateClipboardEvent(snapshot, clipboardOperation), CancellationToken.None);
                return;
            }

            if (virtualKeyCode == VkBack)
            {
                if (_inputBuffer.Length > 0)
                {
                    _inputBuffer.Length--;
                }
                return;
            }

            if (LooksSensitive(snapshot) || !TryGetPrintableCharacter(virtualKeyCode, out var character))
            {
                return;
            }

            var descriptor = DesktopCaptureMapper.Describe(snapshot);
            if (_inputDescriptor is not null && !string.Equals(_inputDescriptor, descriptor, StringComparison.Ordinal))
            {
                FlushInputBuffer();
            }

            _inputDescriptor = descriptor;
            _inputSnapshot = snapshot;
            _inputBuffer.Append(character);
        }
        catch
        {
            // Desktop capture must never block or crash the user's workflow.
        }
    }

    private void FlushInputBuffer()
    {
        if (_inputBuffer.Length == 0 || _inputSnapshot is null)
        {
            _inputBuffer.Clear();
            _inputSnapshot = null;
            _inputDescriptor = null;
            return;
        }

        var text = _inputBuffer.ToString();
        var snapshot = _inputSnapshot;
        snapshot.ScreenshotDataUrl ??= CaptureDesktopScreenshotDataUrl();
        _inputBuffer.Clear();
        _inputSnapshot = null;
        _inputDescriptor = null;
        _ = _handleCaptureEventAsync(DesktopCaptureMapper.CreateInputEvent(snapshot, text), CancellationToken.None);
    }

    private void QueueClick(DesktopInteractionSnapshot snapshot, Point point)
    {
        CaptureEvent? eventToRecord = null;
        lock (_clickLock)
        {
            if (_pendingClickSnapshot is not null && IsDoubleClickCandidate(_pendingClickSnapshot, _pendingClickPoint, snapshot, point))
            {
                ClearPendingClickTimer();
                _pendingClickSnapshot = null;
                eventToRecord = DesktopCaptureMapper.CreateDoubleClickEvent(snapshot);
            }
            else
            {
                if (_pendingClickSnapshot is not null)
                {
                    eventToRecord = DesktopCaptureMapper.CreateClickEvent(_pendingClickSnapshot);
                }

                _pendingClickSnapshot = snapshot;
                _pendingClickPoint = point;
                ClearPendingClickTimer();
                _pendingClickTimer = new System.Threading.Timer(_ => FlushPendingClick(), null, TimeSpan.FromMilliseconds(GetDoubleClickTime()), Timeout.InfiniteTimeSpan);
            }
        }

        if (eventToRecord is not null)
        {
            _ = _handleCaptureEventAsync(eventToRecord, CancellationToken.None);
        }
    }

    private void FlushPendingClick()
    {
        CaptureEvent? eventToRecord = null;
        lock (_clickLock)
        {
            if (_pendingClickSnapshot is null)
            {
                ClearPendingClickTimer();
                return;
            }

            eventToRecord = DesktopCaptureMapper.CreateClickEvent(_pendingClickSnapshot);
            _pendingClickSnapshot = null;
            ClearPendingClickTimer();
        }

        _ = _handleCaptureEventAsync(eventToRecord, CancellationToken.None);
    }

    private void ClearPendingClickTimer()
    {
        _pendingClickTimer?.Dispose();
        _pendingClickTimer = null;
    }

    private static bool IsDoubleClickCandidate(
        DesktopInteractionSnapshot previousSnapshot,
        Point previousPoint,
        DesktopInteractionSnapshot nextSnapshot,
        Point nextPoint)
    {
        var maxX = Math.Max(GetSystemMetrics(SmCxDoubleClk), 4);
        var maxY = Math.Max(GetSystemMetrics(SmCyDoubleClk), 4);
        return Math.Abs(previousPoint.X - nextPoint.X) <= maxX
            && Math.Abs(previousPoint.Y - nextPoint.Y) <= maxY
            && string.Equals(DesktopCaptureMapper.Describe(previousSnapshot), DesktopCaptureMapper.Describe(nextSnapshot), StringComparison.Ordinal);
    }

    private static DesktopInteractionSnapshot CaptureAtPoint(Point point)
    {
        var controlHandle = WindowFromPoint(point);
        var rootHandle = GetAncestor(controlHandle, GaRoot);
        if (rootHandle == IntPtr.Zero)
        {
            rootHandle = controlHandle;
        }

        _ = GetWindowThreadProcessId(rootHandle, out var processId);
        var automationSnapshot = TryCaptureAutomationElement(point);
        return new DesktopInteractionSnapshot
        {
            ProcessId = (int)processId,
            ProcessName = GetProcessName((int)processId),
            WindowTitle = GetWindowText(rootHandle),
            WindowClassName = GetClassName(rootHandle),
            ControlName = FirstNonBlank(automationSnapshot?.Name, GetWindowText(controlHandle)),
            ControlClassName = FirstNonBlank(automationSnapshot?.ControlType, automationSnapshot?.ClassName, GetClassName(controlHandle))
        };
    }

    private static DesktopInteractionSnapshot CaptureFocusedElement()
    {
        var foregroundWindow = GetForegroundWindow();
        _ = GetWindowThreadProcessId(foregroundWindow, out var processId);
        var automationSnapshot = TryCaptureFocusedAutomationElement();

        return new DesktopInteractionSnapshot
        {
            ProcessId = (int)processId,
            ProcessName = GetProcessName((int)processId),
            WindowTitle = GetWindowText(foregroundWindow),
            WindowClassName = GetClassName(foregroundWindow),
            ControlName = automationSnapshot?.Name,
            ControlClassName = FirstNonBlank(automationSnapshot?.ControlType, automationSnapshot?.ClassName)
        };
    }

    private static (string? Name, string? ClassName, string? ControlType)? TryCaptureAutomationElement(Point point)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y));
            return (
                element.Current.Name,
                element.Current.ClassName,
                element.Current.ControlType?.ProgrammaticName);
        }
        catch
        {
            return null;
        }
    }

    private static (string? Name, string? ClassName, string? ControlType)? TryCaptureFocusedAutomationElement()
    {
        try
        {
            var element = AutomationElement.FocusedElement;
            return (
                element.Current.Name,
                element.Current.ClassName,
                element.Current.ControlType?.ProgrammaticName);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsConfirmKey(int virtualKeyCode, out string keyName)
    {
        keyName = virtualKeyCode switch
        {
            VkReturn => "Enter",
            VkTab => "Tab",
            VkEscape => "Escape",
            _ => string.Empty
        };
        return keyName.Length > 0;
    }

    private static bool IsClipboardShortcut(int virtualKeyCode, out string operation)
    {
        operation = virtualKeyCode switch
        {
            VkC when IsControlKeyDown() => "Copy",
            VkV when IsControlKeyDown() => "Paste",
            VkX when IsControlKeyDown() => "Cut",
            _ => string.Empty
        };
        return operation.Length > 0;
    }

    private static bool IsControlKeyDown() =>
        IsKeyDown(VkControl) || IsKeyDown(VkLControl) || IsKeyDown(VkRControl);

    private static bool IsKeyDown(int virtualKeyCode) =>
        (GetAsyncKeyState(virtualKeyCode) & 0x8000) != 0;

    private static bool LooksSensitive(DesktopInteractionSnapshot snapshot)
    {
        var haystack = string.Join(" ", snapshot.ControlName, snapshot.ControlClassName, snapshot.WindowTitle);
        return haystack.Contains("密码", StringComparison.OrdinalIgnoreCase)
            || haystack.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetPrintableCharacter(int virtualKeyCode, out char character)
    {
        character = '\0';
        var keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            return false;
        }

        var scanCode = MapVirtualKey((uint)virtualKeyCode, 0);
        var buffer = new StringBuilder(8);
        var result = ToUnicode((uint)virtualKeyCode, scanCode, keyboardState, buffer, buffer.Capacity, 0);
        if (result <= 0 || buffer.Length == 0)
        {
            return false;
        }

        character = buffer[0];
        return !char.IsControl(character);
    }

    private static string? CaptureDesktopScreenshotDataUrl()
    {
        try
        {
            var bounds = Screen.AllScreens
                .Select(screen => screen.Bounds)
                .Aggregate(Rectangle.Union);

            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);

            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            return $"data:image/png;base64,{Convert.ToBase64String(stream.ToArray())}";
        }
        catch
        {
            return null;
        }
    }

    private static string? GetProcessName(int processId)
    {
        if (processId <= 0)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetWindowText(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var builder = new StringBuilder(512);
        return GetWindowText(handle, builder, builder.Capacity) > 0 ? builder.ToString() : null;
    }

    private static string? GetClassName(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var builder = new StringBuilder(256);
        return GetClassName(handle, builder, builder.Capacity) > 0 ? builder.ToString() : null;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct Point
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MouseHookStruct
    {
        public readonly Point Point;
        public readonly int MouseData;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KeyboardHookStruct
    {
        public readonly int VirtualKeyCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicode(uint virtualKeyCode, uint scanCode, byte[] keyboardState, StringBuilder receivingBuffer, int bufferSize, uint flags);
}
