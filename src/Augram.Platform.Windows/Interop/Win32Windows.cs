using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Augram.Platform.Windows.WindowSystem;

namespace Augram.Platform.Windows.Interop;

/// <summary>
/// The real <see cref="IWin32Windows"/>, <see cref="IWin32Foreground"/> and <see cref="IWin32WindowControl"/>: thin
/// wrappers over <see cref="NativeMethods"/> that turn buffers into strings and Win32 structs into <see cref="ScreenRect"/>.
/// No decisions live here; those are in <c>WindowSystem/</c> where a fake can stand in for this class.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class Win32Windows : IWin32Windows, IWin32Foreground, IWin32WindowControl
{
    private const int MaxClassName = 256;
    private const int MaxTitle = 1024;
    private const int MaxPath = 32768;

    public nint WindowFromPoint(int x, int y) => NativeMethods.WindowFromPoint(new NativeMethods.Point { X = x, Y = y });

    public nint Parent(nint hwnd) => NativeMethods.GetParent(hwnd);

    public nint Root(nint hwnd) => NativeMethods.GetAncestor(hwnd, NativeMethods.GaRoot);

    public nint RootOwner(nint hwnd) => NativeMethods.GetAncestor(hwnd, NativeMethods.GaRootOwner);

    public string ClassName(nint hwnd)
    {
        Span<char> buffer = stackalloc char[MaxClassName];
        var length = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer[..length]) : string.Empty;
    }

    public string Title(nint hwnd)
    {
        Span<char> buffer = stackalloc char[MaxTitle];
        var length = NativeMethods.GetWindowText(hwnd, buffer, buffer.Length);
        return length > 0 ? new string(buffer[..length]) : string.Empty;
    }

    public (uint ThreadId, int ProcessId) ThreadAndProcess(nint hwnd)
    {
        var thread = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return (thread, (int)pid);
    }

    public string? ProcessImagePath(int processId)
    {
        using var process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, (uint)processId);
        if (process.IsInvalid)
        {
            return null;
        }

        var buffer = new char[MaxPath];
        var size = (uint)buffer.Length;
        return NativeMethods.QueryFullProcessImageName(process, 0, buffer, ref size) && size > 0
            ? new string(buffer, 0, (int)size)
            : null;
    }

    public string? ProcessBaseName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public nint FindVisibleChild(nint parent, string className)
    {
        var child = NativeMethods.FindWindowEx(parent, 0, className, null);
        while (child != 0 && !NativeMethods.IsWindowVisible(child))
        {
            child = NativeMethods.FindWindowEx(parent, child, className, null);
        }

        return child;
    }

    public nint ShellWindow() => NativeMethods.GetShellWindow();

    public nint DesktopWindow() => NativeMethods.GetDesktopWindow();

    public bool TryWindowRect(nint hwnd, out ScreenRect rect)
    {
        var ok = NativeMethods.GetWindowRect(hwnd, out var r);
        rect = ok ? new ScreenRect(r.Left, r.Top, r.Right, r.Bottom) : default;
        return ok;
    }

    public bool TryMonitorRect(nint hwnd, out ScreenRect rect)
    {
        var ok = TryMonitorInfo(hwnd, out var info);
        rect = ok ? ToRect(info.Monitor) : default;
        return ok;
    }

    public bool TryWorkArea(nint hwnd, out ScreenRect rect)
    {
        var ok = TryMonitorInfo(hwnd, out var info);
        rect = ok ? ToRect(info.Work) : default;
        return ok;
    }

    public bool IsWindow(nint hwnd) => NativeMethods.IsWindow(hwnd);

    public bool IsIconic(nint hwnd) => NativeMethods.IsIconic(hwnd);

    public bool IsZoomed(nint hwnd) => NativeMethods.IsZoomed(hwnd);

    public bool IsTopmost(nint hwnd)
        => ((long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle) & NativeMethods.WsExTopmost) != 0;

    public unsafe IReadOnlyList<nint> TopLevelWindows()
    {
        var windows = new List<nint>();
        var handle = GCHandle.Alloc(windows);
        try
        {
            NativeMethods.EnumWindows(&CollectWindow, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        return windows;
    }

    public bool IsVisible(nint hwnd) => NativeMethods.IsWindowVisible(hwnd);

    public nint Owner(nint hwnd) => NativeMethods.GetWindow(hwnd, NativeMethods.GwOwner);

    public bool IsToolWindow(nint hwnd)
        => ((long)NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle) & NativeMethods.WsExToolWindow) != 0;

    [UnmanagedCallersOnly]
    private static int CollectWindow(nint hwnd, nint state)
    {
        ((List<nint>)GCHandle.FromIntPtr(state).Target!).Add(hwnd);
        return 1;
    }

    public void ShowWindow(nint hwnd, int command) => NativeMethods.ShowWindow(hwnd, command);

    public bool PostSysCommandClose(nint hwnd) => NativeMethods.PostMessage(hwnd, NativeMethods.WmSysCommand, NativeMethods.ScClose, 0);

    public bool SetTopmost(nint hwnd, bool topmost)
        => NativeMethods.SetWindowPos(hwnd, topmost ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);

    public bool SetWindowBounds(nint hwnd, int x, int y, int width, int height)
        => NativeMethods.SetWindowPos(hwnd, 0, x, y, width, height, NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);

    public int LastError() => Marshal.GetLastPInvokeError();

    public nint ForegroundWindow() => NativeMethods.GetForegroundWindow();

    public bool SetForegroundWindow(nint hwnd) => NativeMethods.SetForegroundWindow(hwnd);

    public bool AttachThreadInput(uint attachFrom, uint attachTo, bool attach) => NativeMethods.AttachThreadInput(attachFrom, attachTo, attach);

    public uint CurrentThreadId() => NativeMethods.GetCurrentThreadId();

    public uint WindowThreadId(nint hwnd) => NativeMethods.GetWindowThreadProcessId(hwnd, out _);

    public void SendAltTap()
    {
        Span<NativeMethods.Input> inputs = stackalloc NativeMethods.Input[2];
        inputs[0].Type = NativeMethods.InputKeyboard;
        inputs[0].Union.Keyboard.VirtualKey = NativeMethods.VkMenu;
        inputs[1].Type = NativeMethods.InputKeyboard;
        inputs[1].Union.Keyboard.VirtualKey = NativeMethods.VkMenu;
        inputs[1].Union.Keyboard.Flags = NativeMethods.KeyEventKeyUp;
        NativeMethods.SendInput((uint)inputs.Length, inputs, Unsafe.SizeOf<NativeMethods.Input>());
    }

    private static bool TryMonitorInfo(nint hwnd, out NativeMethods.MonitorInfo info)
    {
        info = new NativeMethods.MonitorInfo { Size = (uint)Unsafe.SizeOf<NativeMethods.MonitorInfo>() };
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        return monitor != 0 && NativeMethods.GetMonitorInfo(monitor, ref info);
    }

    private static ScreenRect ToRect(NativeMethods.Rect r) => new(r.Left, r.Top, r.Right, r.Bottom);
}
