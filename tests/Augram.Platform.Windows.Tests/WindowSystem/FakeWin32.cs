using Augram.Platform.Windows.WindowSystem;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>
/// Scripted window tree standing in for Win32. Windows are added with their class, parent, root, owner and process;
/// activation success per technique is a switch, so the activator's order and verification can be asserted.
/// </summary>
internal sealed class FakeWin32 : IWin32Windows, IWin32Foreground
{
    private readonly Dictionary<nint, Window> _windows = [];
    private readonly Dictionary<int, (string? Path, string? BaseName)> _processes = [];
    private readonly Dictionary<nint, ScreenRect> _windowRects = [];
    private readonly Dictionary<nint, ScreenRect> _monitorRects = [];

    public nint Foreground { get; set; }

    public nint Shell { get; set; }

    public nint Desktop { get; set; }

    public nint UnderPoint { get; set; }

    public bool PlainSucceeds { get; set; }

    public bool AttachedSucceeds { get; set; }

    public bool AltTapSucceeds { get; set; }

    public uint OurThread { get; set; } = 1;

    public bool Attached { get; private set; }

    public bool AltTapped { get; private set; }

    public List<string> Calls { get; } = [];

    public FakeWin32 AddWindow(nint hwnd, string className, nint parent = 0, nint root = 0, nint owner = 0, int pid = 0, string title = "", uint thread = 0, bool visible = true)
    {
        _windows[hwnd] = new Window(className, parent, root == 0 ? hwnd : root, owner == 0 ? (root == 0 ? hwnd : root) : owner, pid, title, thread, visible);
        return this;
    }

    public FakeWin32 AddProcess(int pid, string? path, string? baseName = null)
    {
        _processes[pid] = (path, baseName);
        return this;
    }

    public FakeWin32 WithRects(nint hwnd, ScreenRect window, ScreenRect monitor)
    {
        _windowRects[hwnd] = window;
        _monitorRects[hwnd] = monitor;
        return this;
    }

    public nint WindowFromPoint(int x, int y) => UnderPoint;

    public nint Parent(nint hwnd) => Get(hwnd)?.Parent ?? 0;

    public nint Root(nint hwnd) => Get(hwnd)?.Root ?? 0;

    public nint RootOwner(nint hwnd) => Get(hwnd)?.Owner ?? 0;

    public string ClassName(nint hwnd) => Get(hwnd)?.ClassName ?? string.Empty;

    public string Title(nint hwnd) => Get(hwnd)?.Title ?? string.Empty;

    public (uint ThreadId, int ProcessId) ThreadAndProcess(nint hwnd)
    {
        var w = Get(hwnd);
        return w is null ? (0, 0) : (w.Thread, w.Pid);
    }

    public string? ProcessImagePath(int processId) => _processes.TryGetValue(processId, out var p) ? p.Path : null;

    public string? ProcessBaseName(int processId) => _processes.TryGetValue(processId, out var p) ? p.BaseName : null;

    public nint FindVisibleChild(nint parent, string className)
    {
        foreach (var (hwnd, w) in _windows)
        {
            if (w.Parent == parent && w.Visible && w.ClassName == className)
            {
                return hwnd;
            }
        }

        return 0;
    }

    public nint ShellWindow() => Shell;

    public nint DesktopWindow() => Desktop;

    public bool TryWindowRect(nint hwnd, out ScreenRect rect) => _windowRects.TryGetValue(hwnd, out rect);

    public bool TryMonitorRect(nint hwnd, out ScreenRect rect) => _monitorRects.TryGetValue(hwnd, out rect);

    public nint ForegroundWindow() => Foreground;

    public bool SetForegroundWindow(nint hwnd)
    {
        Calls.Add($"set:{hwnd}:attached={Attached}:alt={AltTapped}");
        var allowed = PlainSucceeds || (Attached && AttachedSucceeds) || (AltTapped && AltTapSucceeds);
        if (allowed)
        {
            Foreground = hwnd;
        }

        return allowed;
    }

    public bool AttachThreadInput(uint attachFrom, uint attachTo, bool attach)
    {
        Calls.Add($"attach:{attachFrom}->{attachTo}:{attach}");
        Attached = attach;
        return true;
    }

    public uint CurrentThreadId() => OurThread;

    public uint WindowThreadId(nint hwnd) => Get(hwnd)?.Thread ?? 0;

    public void SendAltTap()
    {
        Calls.Add("alt-tap");
        AltTapped = true;
    }

    private Window? Get(nint hwnd) => _windows.TryGetValue(hwnd, out var w) ? w : null;

    private sealed record Window(string ClassName, nint Parent, nint Root, nint Owner, int Pid, string Title, uint Thread, bool Visible);
}
