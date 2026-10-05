using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Engine.Hosting;

namespace Augram.Engine.Input;

/// <summary>
/// Owns the <see cref="IInputSource"/> lifecycle and reinstalls it when it dies (N4, learnings 0001 B3).
/// Two signals: the source's own <see cref="HookHealthKind.Lost"/> (SharpHook's HookDisabled or a failed
/// hook loop), and the watchdog, when an <see cref="ICursorProbe"/> is present: no hook event for
/// <see cref="DeadAfterMs"/> while the cursor was seen moving in at least <see cref="MinCursorMoves"/> polls.
/// Session and power events (<see cref="ISystemEvents"/>) are logged and restart the silence clock, since
/// after sleep or lock the OS owes no events; resume and unlock also raise <see cref="ResetRequested"/> so
/// the capture forgets state the OS may no longer share. Reinstalls never happen inside a source callback
/// (that can run on the hook thread being torn down); they wait for the next <see cref="Poll"/>, which the
/// timer calls every <see cref="PollInterval"/> and tests call directly.
/// </summary>
public sealed class HookHealthMonitor : IDisposable
{
    public const int DeadAfterMs = 15_000;
    public const int MinCursorMoves = 4;
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(1);
    private const int MaxRetryBackoffMs = 60_000;
    private const int MinuteMs = 60_000;

    private readonly IInputSource _source;
    private readonly IClock _clock;
    private readonly IEventLog _log;
    private readonly ICursorProbe? _cursor;
    private readonly ISystemEvents? _system;
    private readonly IDisposable? _healthRegistration;
    private readonly long[] _countHistory;
    private readonly object _gate = new();
    private Timer? _timer;
    private InputHandler? _wrapped;
    private long _lastEventMs;
    private long _lastSeenEventMs;
    private long _eventCount;
    private long _installedAtMs;
    private long _retryAtMs;
    private int _lostPending;
    private int _retries;
    private int _movesSinceEvent;
    private int _historyIndex;
    private int _generation;
    private int _reinstalls;
    private bool _firstEventPending;
    private bool _running;
    private (int X, int Y) _lastCursor;
    private DateTimeOffset? _aliveSince;

    public HookHealthMonitor(
        IInputSource source,
        IClock clock,
        IEventLog log,
        ICursorProbe? cursor = null,
        ISystemEvents? system = null,
        HealthRegistry? health = null,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);
        _source = source;
        _clock = clock;
        _log = log;
        _cursor = cursor;
        _system = system;
        PollInterval = pollInterval ?? DefaultPollInterval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(PollInterval, TimeSpan.Zero);
        _countHistory = new long[Math.Max(1, (int)(MinuteMs / PollInterval.TotalMilliseconds)) + 1];
        _source.HookHealthChanged += OnHookHealthChanged;
        if (_system is not null)
        {
            _system.Occurred += OnSystemEvent;
        }

        _healthRegistration = health?.Register(snapshot => snapshot with
        {
            HookAliveSince = _aliveSince,
            HookReinstallCount = ReinstallCount,
            EventsLastMinute = EventsLastMinute,
        });
    }

    /// <summary>Raised after a reinstall, resume or unlock, on the poll thread or the OS event thread. Reason text for the log.</summary>
    public event EventHandler<string>? ResetRequested;

    public TimeSpan PollInterval { get; }

    public int Generation => Volatile.Read(ref _generation);

    public int ReinstallCount => Volatile.Read(ref _reinstalls);

    public long EventCount => Volatile.Read(ref _eventCount);

    public int EventsLastMinute => (int)(EventCount - Volatile.Read(ref _countHistory[_historyIndex]));

    public void Start(InputHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (_running)
            {
                throw new InvalidOperationException("Already started.");
            }

            _running = true;
            _wrapped = (in RawInput input) =>
            {
                Volatile.Write(ref _lastEventMs, input.TimestampMs);
                Interlocked.Increment(ref _eventCount);
                return handler(in input);
            };
            Install("start");
            _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            _running = false;
            _timer?.Dispose();
            _timer = null;
            _source.Stop();
            _log.Info(LogSources.Hook, "Hook stopped", ("generation", Generation));
        }
    }

    /// <summary>One watchdog cycle. The timer calls it; tests call it directly.</summary>
    public void Poll()
    {
        lock (_gate)
        {
            if (!_running)
            {
                return;
            }

            var now = _clock.MonotonicMs;
            var lastEvent = Volatile.Read(ref _lastEventMs);
            RecordHistory();
            if (_firstEventPending && lastEvent > _installedAtMs)
            {
                _firstEventPending = false;
                _log.Info(LogSources.Hook, "Hook alive", ("generation", Generation), ("firstEventMs", lastEvent - _installedAtMs));
            }

            if (Volatile.Read(ref _lostPending) != 0)
            {
                if (now >= _retryAtMs)
                {
                    Reinstall("hook reported lost", now);
                }

                return;
            }

            if (_cursor is null)
            {
                return;
            }

            if (lastEvent != _lastSeenEventMs)
            {
                _lastSeenEventMs = lastEvent;
                _movesSinceEvent = 0;
            }

            if (_cursor.TryGetPosition(out var x, out var y) && (x, y) != _lastCursor)
            {
                _lastCursor = (x, y);
                _movesSinceEvent++;
            }

            var silentMs = now - lastEvent;
            if (silentMs > DeadAfterMs && _movesSinceEvent >= MinCursorMoves)
            {
                Reinstall($"no hook event for {silentMs} ms while the cursor moved in {_movesSinceEvent} polls", now);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _source.HookHealthChanged -= OnHookHealthChanged;
        if (_system is not null)
        {
            _system.Occurred -= OnSystemEvent;
        }

        _healthRegistration?.Dispose();
    }

    private void RecordHistory()
    {
        _countHistory[_historyIndex] = EventCount;
        _historyIndex = (_historyIndex + 1) % _countHistory.Length;
    }

    private void Install(string reason)
    {
        var now = _clock.MonotonicMs;
        Interlocked.Increment(ref _generation);
        _installedAtMs = now;
        _firstEventPending = true;
        _movesSinceEvent = 0;
        Volatile.Write(ref _lastEventMs, now);
        _lastSeenEventMs = now;
        _log.Info(LogSources.Hook, "Hook installing", ("generation", Generation), ("reason", reason));
        _source.Start(_wrapped!);
    }

    private void Reinstall(string reason, long now)
    {
        _log.Warning(LogSources.Hook, "Hook lost, reinstalling", ("generation", Generation), ("reason", reason));
        Volatile.Write(ref _lostPending, 0);
        var started = _clock.MonotonicMs;
        try
        {
            _source.Stop();
            Install("reinstall");
        }
        catch (Exception e)
        {
            _retries++;
            _retryAtMs = now + Math.Min(MaxRetryBackoffMs, 1000 << Math.Min(_retries, 6));
            Volatile.Write(ref _lostPending, 1);
            _log.Error(LogSources.Hook, "Hook reinstall failed", e, ("generation", Generation), ("retryInMs", _retryAtMs - now));
            return;
        }

        _retries = 0;
        Interlocked.Increment(ref _reinstalls);
        _log.Info(LogSources.Hook, "Hook reinstalled", ("generation", Generation), ("reinstalls", ReinstallCount), ("tookMs", _clock.MonotonicMs - started));
        ResetRequested?.Invoke(this, reason);
    }

    private void OnHookHealthChanged(object? sender, HookHealth e)
    {
        switch (e.Kind)
        {
            case HookHealthKind.Installed:
                _aliveSince = _clock.UtcNow;
                _log.Info(LogSources.Hook, "Hook installed", ("generation", e.Generation));
                break;
            case HookHealthKind.Stopped:
                _log.Debug(LogSources.Hook, "Hook stopped by request", ("generation", e.Generation), ("detail", e.Detail));
                break;
            case HookHealthKind.Lost:
                _aliveSince = null;
                Volatile.Write(ref _lostPending, 1);
                _log.Warning(LogSources.Hook, "Hook lost", ("generation", e.Generation), ("detail", e.Detail));
                break;
        }
    }

    private void OnSystemEvent(object? sender, SystemEventKind kind)
    {
        _log.Info(LogSources.Hook, "System event", ("kind", kind));
        Volatile.Write(ref _lastEventMs, _clock.MonotonicMs);
        if (kind is SystemEventKind.Resumed or SystemEventKind.SessionUnlocked)
        {
            ResetRequested?.Invoke(this, kind.ToString());
        }
    }
}
