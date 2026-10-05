using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Augram.Platform.Windows.WindowSystem;

namespace Augram.Platform.Windows.Interop;

/// <summary>
/// The real <see cref="IWin32Windows"/> and <see cref="IWin32Foreground"/>: thin wrappers over
/// <see cref="NativeMethods"/> that turn buffers into strings and Win32 structs into <see cref="ScreenRect"/>.
/// No decisions live here; those are in <c>WindowSystem/</c> where a fake can stand in for this class.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class Win32Windows : IWin32Windows, IWin32Foreground
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
        var info = new NativeMethods.MonitorInfo { Size = (uint)Unsafe.SizeOf<NativeMethods.MonitorInfo>() };
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        var ok = monitor != 0 && NativeMethods.GetMonitorInfo(monitor, ref info);
        rect = ok ? new ScreenRect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom) : default;
        return ok;
    }

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
}
