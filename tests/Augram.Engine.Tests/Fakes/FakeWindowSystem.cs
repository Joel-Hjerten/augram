using System.Diagnostics;
using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

/// <summary>
/// An <see cref="IWindowSystem"/> the test configures: <see cref="WindowAt"/> answers <see cref="Window"/>
/// (null by default, nothing under the point), <see cref="Activate"/> answers <see cref="ActivationResult"/>
/// and records the call with a <see cref="Stopwatch"/> timestamp so the settle delay can be measured.
/// Thread-safe because the executor calls it.
/// </summary>
internal sealed class FakeWindowSystem : IWindowSystem
{
    private readonly object _gate = new();
    private readonly List<(int X, int Y)> _lookups = [];
    private readonly List<(WindowIdentity Target, long Timestamp)> _activations = [];

    public WindowIdentity? Window { get; set; }

    public WindowIdentity? ForegroundWindow { get; set; }

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

    public WindowIdentity? Foreground() => ForegroundWindow;

    public ActivationResult Activate(WindowIdentity target)
    {
        lock (_gate)
        {
            _activations.Add((target, Stopwatch.GetTimestamp()));
            return ActivationResult;
        }
    }
}
