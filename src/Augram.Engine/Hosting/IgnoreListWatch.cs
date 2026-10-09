using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;

namespace Augram.Engine.Hosting;

/// <summary>
/// Keeps the pointer's answer current for the hook, on its own thread (<c>augram-ignore-watch</c>): the ignore list's (F5;
/// SP.net's Ignore List) and the anchor plan for the window under the pointer (trigger combinations, per app: Joel
/// 2026-10-09), so the hook reads one volatile at button-down and never looks a window up (CLAUDE.md invariant 1). The hook
/// hands it each move's position (<see cref="PointerAt"/>: one volatile write, and one wake per batch of moves); it also
/// wakes on <see cref="Wake"/> (the mapping or the stroke button changed), on resume, unlock and display changes (every key
/// is dropped), and every <see cref="FocusPollInterval"/> while anything is watched, to see focus move (focus is asked at
/// that pace only, however busy the pointer). Each pass (at most one per <see cref="PassInterval"/>, so a busy pointer costs
/// a bounded number of passes) is an <see cref="IgnoreLookup"/>; its answer goes to <see cref="InputGate.PublishPointer"/>. The pointer entering and leaving an ignored app is logged at Debug, a pause
/// starting and stopping at Info, and a pause (or its app's name) changing raises the callback the host turns into
/// <c>EngineHost.PauseChanged</c>. Present only when the engine has a mapping.
/// </summary>
internal sealed class IgnoreListWatch : IDisposable
{
    /// <summary>The shortest time between two passes: the pointer's staleness bound, apart from the lookup itself.</summary>
    public static readonly TimeSpan PassInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>How often focus is checked while anything is watched (a window that takes focus under a motionless pointer is seen this late at most).</summary>
    public static readonly TimeSpan FocusPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>On a platform without cheap window keys, the shortest time between two pointer lookups.</summary>
    public static readonly TimeSpan SlowLookupInterval = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly InputGate _gate;
    private readonly IgnoreLookup _lookup;
    private readonly Func<MappingDocument> _mapping;
    private readonly IEventLog _log;
    private readonly IClock _clock;
    private readonly ICursorProbe? _cursor;
    private readonly ISystemEvents? _systemEvents;
    private readonly Action<IgnoredApp?> _pauseChanged;
    private readonly SemaphoreSlim _wake = new(0);
    private readonly Thread _thread;
    private long _pointer = IgnoreLookup.NoPointer;
    private int _wakePending;
    private int _forget;
    private int _watchesPointer;
    private int _started;
    private volatile bool _stopping;
    private IgnoredApp? _over;
    private IgnoredApp? _pausedBy;
    private bool _failing;
    private long _focusCheckedAt = -(long)FocusPollInterval.TotalMilliseconds;

    public IgnoreListWatch(InputGate gate, EnginePorts ports, Func<MappingDocument> mapping, Action<IgnoredApp?> pauseChanged)
    {
        _gate = gate;
        _mapping = mapping;
        _log = ports.Log;
        _clock = ports.Clock;
        _cursor = ports.CursorProbe;
        _systemEvents = ports.SystemEvents;
        _pauseChanged = pauseChanged;
        _lookup = new IgnoreLookup(ports.Windows, ports.WindowOperations.Platform, ports.Clock, SlowLookupInterval);
        _thread = new Thread(Run) { IsBackground = true, Name = "augram-ignore-watch" };
    }

    /// <summary>An active ignored app can match here: a move wakes the watch only then.</summary>
    public bool WatchesPointer => Volatile.Read(ref _watchesPointer) != 0;

    /// <summary>The ignored app under the pointer as of the last pass, or null.</summary>
    public IgnoredApp? Over => Volatile.Read(ref _over);

    /// <summary>The focused "disable while focused" app as of the last pass, or null.</summary>
    public IgnoredApp? PausedBy => Volatile.Read(ref _pausedBy);

    /// <summary>
    /// Hook thread, every move and press: the latest position (kept even while nothing is watched, so a mapping change can
    /// judge the pointer at once), and a wake while something is watched, unless one is pending already.
    /// </summary>
    public void PointerAt(int x, int y)
    {
        Volatile.Write(ref _pointer, IgnoreLookup.Pack(x, y));
        if (WatchesPointer)
        {
            Wake();
        }
    }

    /// <summary>Any thread: run a pass soon (the mapping changed). Never blocks; does nothing once stopping.</summary>
    public void Wake()
    {
        if (!_stopping && Interlocked.Exchange(ref _wakePending, 1) == 0)
        {
            _wake.Release();
        }
    }

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return;
        }

        if (_systemEvents is not null)
        {
            _systemEvents.Occurred += OnSystemEvent;
        }

        _thread.Start();
        Wake();
    }

    /// <summary>Ends the thread and clears the answer: nothing passes through for the ignore list once the engine stops.</summary>
    public void Stop()
    {
        if (Volatile.Read(ref _started) == 0 || _stopping)
        {
            return;
        }

        _stopping = true;
        _wake.Release();
        if (_systemEvents is not null)
        {
            _systemEvents.Occurred -= OnSystemEvent;
        }

        if (_thread != Thread.CurrentThread)
        {
            _thread.Join(StopTimeout);
        }

        _gate.PublishPointer(0, AnchorPlan.None);
        Volatile.Write(ref _over, null);
        if (Interlocked.Exchange(ref _pausedBy, null) is not null)
        {
            _pauseChanged(null);
        }
    }

    /// <summary>
    /// <see cref="Stop"/>. The semaphore is left undisposed on purpose: it never allocates an OS handle (its wait handle is
    /// never asked for), and a wake racing the stop (the hook's last move, a mapping change) then finds it still usable.
    /// </summary>
    public void Dispose() => Stop();

    private void Run()
    {
        while (true)
        {
            _wake.Wait(WaitMs());
            if (_stopping)
            {
                return;
            }

            Interlocked.Exchange(ref _wakePending, 0);
            Pass();
            // A busy pointer wakes the watch at most once per interval; the latest position waits in _pointer.
            Thread.Sleep(PassInterval);
        }
    }

    private int WaitMs()
    {
        if (!_lookup.WatchesPointer)
        {
            return Timeout.Infinite;
        }

        // Until the next focus check is due, or the put-off pointer lookup.
        var poll = (long)FocusPollInterval.TotalMilliseconds;
        var wait = Math.Clamp(poll - (Environment.TickCount64 - _focusCheckedAt), 0, poll);
        if (_lookup.RetryAtMs is { } retryAt)
        {
            wait = Math.Clamp(retryAt - _clock.MonotonicMs, 0, wait);
        }

        return (int)wait;
    }

    private void Pass()
    {
        var pointer = Volatile.Read(ref _pointer);
        if (pointer == IgnoreLookup.NoPointer && _cursor is not null && _cursor.TryGetPosition(out var x, out var y))
        {
            // Nothing moved since the engine started: where the cursor is now.
            pointer = IgnoreLookup.Pack(x, y);
        }

        // Focus is asked at the polling pace, not on every pass a busy pointer causes (on macOS each ask is an Accessibility call).
        var now = Environment.TickCount64;
        var focusDue = now - _focusCheckedAt >= (long)FocusPollInterval.TotalMilliseconds;
        var watched = _lookup.WatchesPointer;
        try
        {
            _lookup.Pass(_mapping(), pointer, Interlocked.Exchange(ref _forget, 0) != 0, focusDue, _gate.StrokeButton);
            if (_lookup.FocusChecked)
            {
                _focusCheckedAt = now;
            }
        }
        catch (Exception exception)
        {
            // An adapter that throws keeps the last answer; said once until a pass succeeds again.
            if (!_failing)
            {
                _failing = true;
                _log.Error(LogSources.Ignore, "Ignore list lookup failed; keeping the last answer", exception);
            }

            return;
        }

        _failing = false;
        if (watched != _lookup.WatchesPointer)
        {
            Volatile.Write(ref _watchesPointer, _lookup.WatchesPointer ? 1 : 0);
            _log.Info(LogSources.Ignore, _lookup.WatchesPointer ? "Ignore list watched" : "Ignore list not watched", ("focus", _lookup.WatchesFocus), ("anchors", _lookup.WatchesAnchors));
        }

        Publish(_lookup.Over, _lookup.PausedBy, _lookup.Plan);
    }

    private void Publish(IgnoredApp? over, IgnoredApp? pausedBy, AnchorPlan plan)
    {
        _gate.PublishPointer((over is null ? 0 : InputGate.OverIgnoredApp) | (pausedBy is null ? 0 : InputGate.PausedByFocus), plan);

        var lastOver = Volatile.Read(ref _over);
        Volatile.Write(ref _over, over);
        if (over?.Id != lastOver?.Id)
        {
            if (lastOver is not null)
            {
                _log.Debug(LogSources.Ignore, "Pointer left an ignored app", ("app", lastOver.Name));
            }

            if (over is not null)
            {
                _log.Debug(LogSources.Ignore, "Pointer over an ignored app; the stroke button passes through", ("app", over.Name));
            }
        }

        var lastPaused = Volatile.Read(ref _pausedBy);
        Volatile.Write(ref _pausedBy, pausedBy);
        if (pausedBy?.Id != lastPaused?.Id)
        {
            if (lastPaused is not null)
            {
                _log.Info(LogSources.Ignore, "Resumed: the ignored app lost focus", ("app", lastPaused.Name));
            }

            if (pausedBy is not null)
            {
                _log.Info(LogSources.Ignore, "Paused while an ignored app has focus", ("app", pausedBy.Name));
            }
        }

        if (pausedBy?.Id != lastPaused?.Id || !string.Equals(pausedBy?.Name, lastPaused?.Name, StringComparison.Ordinal))
        {
            _pauseChanged(pausedBy);
        }
    }

    private void OnSystemEvent(object? sender, SystemEventKind kind)
    {
        if (kind is SystemEventKind.SessionUnlocked or SystemEventKind.Resumed or SystemEventKind.DisplayChanged)
        {
            Volatile.Write(ref _forget, 1);
            Wake();
        }
    }
}
