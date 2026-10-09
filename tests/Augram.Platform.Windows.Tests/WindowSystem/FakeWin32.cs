using Augram.Platform.Windows.WindowSystem;

namespace Augram.Platform.Windows.Tests.WindowSystem;

/// <summary>
/// Scripted window tree standing in for Win32. Windows are added with their class, parent, root, owner and process;
/// activation success per technique is a switch, so the activator's order and verification can be asserted.
/// Window state (iconic, zoomed, topmost) is scripted with <see cref="WithState"/> and follows the commands the
/// operations adapter issues, which are recorded in <see cref="Calls"/>; <see cref="Error"/> is what a failed
/// command reports.
/// </summary>
internal sealed class FakeWin32 : IWin32Windows, IWin32Foreground, IWin32WindowControl
{
    private readonly Dictionary<nint, Window> _windows = [];
    private readonly Dictionary<int, (string? Path, string? BaseName)> _processes = [];
    private readonly Dictionary<nint, ScreenRect> _windowRects = [];
    private readonly Dictionary<nint, ScreenRect> _monitorRects = [];
    private readonly Dictionary<nint, ScreenRect> _workAreas = [];
    private readonly Dictionary<nint, State> _states = [];

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

    public bool PostFails { get; set; }

    public bool SetWindowPosFails { get; set; }

    public int Error { get; set; } = 5;

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

    public FakeWin32 WithRects(nint hwnd, ScreenRect window, ScreenRect monitor, ScreenRect? work = null)
    {
        _windowRects[hwnd] = window;
        _monitorRects[hwnd] = monitor;
        _workAreas[hwnd] = work ?? monitor;
        return this;
    }

    /// <summary>While zoomed, <see cref="TryWindowRect"/> reports <paramref name="maximizedRect"/>; a restore brings the scripted rect back.</summary>
    public FakeWin32 WithState(nint hwnd, bool iconic = false, bool zoomed = false, bool topmost = false, ScreenRect? maximizedRect = null)
    {
        _states[hwnd] = new State { Iconic = iconic, Zoomed = zoomed, Topmost = topmost, Maximized = maximizedRect };
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

    public bool TryWindowRect(nint hwnd, out ScreenRect rect)
    {
        if (StateOf(hwnd) is { Zoomed: true, Maximized: { } maximized })
        {
            rect = maximized;
            return true;
        }

        return _windowRects.TryGetValue(hwnd, out rect);
    }

    public bool TryMonitorRect(nint hwnd, out ScreenRect rect) => _monitorRects.TryGetValue(hwnd, out rect);

    public bool TryWorkArea(nint hwnd, out ScreenRect rect) => _workAreas.TryGetValue(hwnd, out rect);

    public bool IsWindow(nint hwnd) => _windows.ContainsKey(hwnd);

    public bool IsIconic(nint hwnd) => StateOf(hwnd)?.Iconic ?? false;

    public bool IsZoomed(nint hwnd) => StateOf(hwnd)?.Zoomed ?? false;

    public bool IsTopmost(nint hwnd) => StateOf(hwnd)?.Topmost ?? false;

    /// <summary>The added windows that are their own root, in the order added (front to back).</summary>
    public IReadOnlyList<nint> TopLevelWindows() => [.. _windows.Where(entry => entry.Value.Root == entry.Key).Select(entry => entry.Key)];

    public bool IsVisible(nint hwnd) => Get(hwnd)?.Visible ?? false;

    /// <summary>An owner given to <see cref="AddWindow"/> that is not the window itself.</summary>
    public nint Owner(nint hwnd) => Get(hwnd) is { } window && window.Owner != hwnd && window.Owner != window.Root ? window.Owner : 0;

    public HashSet<nint> ToolWindows { get; } = [];

    public bool IsToolWindow(nint hwnd) => ToolWindows.Contains(hwnd);

    public void ShowWindow(nint hwnd, int command)
    {
        Calls.Add($"show:{hwnd}:{command}");
        var state = _states[hwnd] = StateOf(hwnd) ?? new State();
        state.Zoomed = command == 3;
        state.Iconic = command == 6;
    }

    public bool PostSysCommandClose(nint hwnd)
    {
        Calls.Add($"close:{hwnd}");
        return !PostFails;
    }

    public bool SetTopmost(nint hwnd, bool topmost)
    {
        Calls.Add($"topmost:{hwnd}:{topmost}");
        if (SetWindowPosFails)
        {
            return false;
        }

        (_states[hwnd] = StateOf(hwnd) ?? new State()).Topmost = topmost;
        return true;
    }

    public bool SetWindowBounds(nint hwnd, int x, int y, int width, int height)
    {
        Calls.Add($"bounds:{hwnd}:{x},{y},{width}x{height}");
        if (SetWindowPosFails)
        {
            return false;
        }

        _windowRects[hwnd] = new ScreenRect(x, y, x + width, y + height);
        return true;
    }

    public int LastError() => Error;

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

    private State? StateOf(nint hwnd) => _states.TryGetValue(hwnd, out var s) ? s : null;

    private sealed record Window(string ClassName, nint Parent, nint Root, nint Owner, int Pid, string Title, uint Thread, bool Visible);

    private sealed class State
    {
        public bool Iconic;
        public bool Zoomed;
        public bool Topmost;
        public ScreenRect? Maximized;
    }
}
