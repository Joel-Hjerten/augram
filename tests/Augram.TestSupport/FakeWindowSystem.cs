using System.Diagnostics;
using Augram.Core.Abstractions;

namespace Augram.TestSupport;

/// <summary>
/// An <see cref="IWindowSystem"/> the test configures. <see cref="WindowAt"/> answers the first window placed with
/// <see cref="Around"/> whose rectangle holds the point (front to back), else <see cref="Window"/> (null by default, nothing
/// under the point); <see cref="Foreground"/> answers <see cref="ForegroundWindow"/>; <see cref="Activate"/> answers
/// <see cref="ActivationResult"/> and records the call with a <see cref="Stopwatch"/> timestamp so the settle delay can be
/// measured. The cheap keys are those windows' handles (0 for none), or null for every call while <see cref="CheapKeys"/> is
/// off. Every call is counted and the points asked are kept. Reads no real window and activates nothing. Thread-safe because
/// the executor and the ignore-list watch call it.
/// </summary>
public sealed class FakeWindowSystem : IWindowSystem
{
    private readonly object _gate = new();
    private readonly List<(int X, int Y)> _lookups = [];
    private readonly List<(WindowIdentity Target, long Timestamp)> _activations = [];
    private readonly List<(int Left, int Top, int Width, int Height, WindowIdentity Window)> _placed = [];
    private WindowIdentity? _window;
    private WindowIdentity? _foreground;
    private int _keyLookups;
    private int _foregroundLookups;
    private int _foregroundKeyLookups;

    public WindowIdentity? Window
    {
        get => Volatile.Read(ref _window);
        set => Volatile.Write(ref _window, value);
    }

    public WindowIdentity? ForegroundWindow
    {
        get => Volatile.Read(ref _foreground);
        set => Volatile.Write(ref _foreground, value);
    }

    /// <summary>Off: both key members answer null, as on a platform without cheap keys.</summary>
    public bool CheapKeys { get; set; } = true;

    public ActivationResult ActivationResult { get; set; } = ActivationResult.NotNeeded;

    /// <summary>The points <see cref="WindowAt"/> was asked, in order.</summary>
    public IReadOnlyList<(int X, int Y)> Lookups
    {
        get
        {
            lock (_gate)
            {
                return [.. _lookups];
            }
        }
    }

    public int KeyLookups => Volatile.Read(ref _keyLookups);

    public int ForegroundLookups => Volatile.Read(ref _foregroundLookups);

    public int ForegroundKeyLookups => Volatile.Read(ref _foregroundKeyLookups);

    public IReadOnlyList<(WindowIdentity Target, long Timestamp)> Activations
    {
        get
        {
            lock (_gate)
            {
                return [.. _activations];
            }
        }
    }

    /// <summary>A Notepad window (the engine tests' default target).</summary>
    public static WindowIdentity Identity(string processName = "notepad.exe", nint handle = 0x1234, nint root = 0x1000)
        => new(handle, root, processName, null, "Untitled", ["Notepad"], ProcessId: 42, IsFullScreen: false, IsDesktop: false);

    /// <summary>Another app's window as the platform would report it, its own root.</summary>
    public static WindowIdentity WindowOf(string process, string? title = null, string? path = null, IReadOnlyList<string>? classes = null, nint handle = 0x100, int processId = 4242)
        => new(handle, handle, process, path, title, classes ?? [], processId, IsFullScreen: false, IsDesktop: false);

    /// <summary>A window of the test process itself, as Augram's own settings window reads.</summary>
    public static WindowIdentity OwnWindow() => WindowOf("testhost.exe", "Augram", handle: 0x900, processId: Environment.ProcessId);

    /// <summary>Puts <paramref name="window"/> on screen, centred on <paramref name="centreX"/>, <paramref name="centreY"/>, <paramref name="size"/> wide and high.</summary>
    public FakeWindowSystem Around(int centreX, int centreY, WindowIdentity window, int size = 100)
    {
        lock (_gate)
        {
            _placed.Add((centreX - size / 2, centreY - size / 2, size, size, window));
        }

        return this;
    }

    public WindowIdentity? WindowAt(int x, int y)
    {
        lock (_gate)
        {
            _lookups.Add((x, y));
            return Find(x, y);
        }
    }

    public WindowIdentity? Foreground()
    {
        Interlocked.Increment(ref _foregroundLookups);
        return ForegroundWindow;
    }

    public nint? WindowKeyAt(int x, int y)
    {
        Interlocked.Increment(ref _keyLookups);
        lock (_gate)
        {
            return CheapKeys ? Find(x, y)?.Handle ?? 0 : null;
        }
    }

    public nint? ForegroundKey()
    {
        Interlocked.Increment(ref _foregroundKeyLookups);
        return CheapKeys ? ForegroundWindow?.Handle ?? 0 : null;
    }

    public ActivationResult Activate(WindowIdentity target)
    {
        lock (_gate)
        {
            _activations.Add((target, Stopwatch.GetTimestamp()));
            return ActivationResult;
        }
    }

    /// <summary>Under the gate.</summary>
    private WindowIdentity? Find(int x, int y)
    {
        foreach (var (left, top, width, height, window) in _placed)
        {
            if (x >= left && y >= top && x < left + width && y < top + height)
            {
                return window;
            }
        }

        return Window;
    }
}
