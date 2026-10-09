using System.Diagnostics;
using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

/// <summary>
/// An <see cref="IWindowSystem"/> the test configures: <see cref="WindowAt"/> answers <see cref="Window"/>
/// (null by default, nothing under the point), <see cref="Foreground"/> answers <see cref="ForegroundWindow"/>,
/// <see cref="Activate"/> answers <see cref="ActivationResult"/> and records the call with a <see cref="Stopwatch"/>
/// timestamp so the settle delay can be measured. The cheap keys are those windows' handles (0 for none), or null
/// for every call while <see cref="CheapKeys"/> is off. Every call is counted. Thread-safe because the executor and the
/// ignore-list watch call it.
/// </summary>
internal sealed class FakeWindowSystem : IWindowSystem
{
    private readonly object _gate = new();
    private readonly List<(int X, int Y)> _lookups = [];
    private readonly List<(WindowIdentity Target, long Timestamp)> _activations = [];
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

    public static WindowIdentity Identity(string processName = "notepad.exe", nint handle = 0x1234, nint root = 0x1000)
        => new(handle, root, processName, null, "Untitled", ["Notepad"], ProcessId: 42, IsFullScreen: false, IsDesktop: false);

    public WindowIdentity? WindowAt(int x, int y)
    {
        lock (_gate)
        {
            _lookups.Add((x, y));
            return Window;
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
        return CheapKeys ? Window?.Handle ?? 0 : null;
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
}
