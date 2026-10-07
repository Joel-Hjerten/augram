using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Engine.Hosting;

/// <summary>
/// The engine side of hotkey capture (F5): at most one armed capture, whose flag in the
/// <see cref="InputGate"/> makes the hook thread suppress key presses system-wide (mouse untouched). Key
/// events reach the caller on the worker thread through <see cref="Deliver"/>. Safety invariant: the
/// flag is cleared at once on dispose, on the idle watchdog (no key event for the caller's timeout), on a
/// hook reset (reinstall, resume, unlock), on engine stop and when another capture replaces this one;
/// every end but dispose is reported to the caller as <see cref="KeyCaptureEventKind.Released"/>, posted
/// to the worker so it arrives after the key events queued before it. Keys pressed during the capture
/// keep their release suppressed afterwards (<c>Input/KeySuppressionShadow</c>). Acts first, logs second.
/// </summary>
internal sealed class KeyCaptureController
{
    private readonly InputGate _gate;
    private readonly IEventLog _log;
    private readonly object _lock = new();
    private Session? _current;

    public KeyCaptureController(InputGate gate, IEventLog log)
    {
        _gate = gate;
        _log = log;
    }

    /// <summary>Arms a capture for <paramref name="callback"/>, replacing any other; disposing the result releases it.</summary>
    public IDisposable Arm(Action<KeyCaptureEvent> callback, TimeSpan idleTimeout)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(idleTimeout, TimeSpan.Zero);
        var session = new Session(this, callback, idleTimeout);
        Session? replaced;
        lock (_lock)
        {
            replaced = _current;
            replaced?.StopWatchdog();
            _current = session;
            _gate.CaptureKeys(true);
            session.StartWatchdog();
        }

        if (replaced is not null)
        {
            Ended(replaced, KeyCaptureEvent.ReplacedReason, notify: true);
        }

        _log.Info(LogSources.Engine, "Key capture armed", ("idleTimeoutMs", idleTimeout.TotalMilliseconds));
        return session;
    }

    /// <summary>Worker thread: one key event the hook reported while armed; restarts the watchdog. Dropped when nothing is armed any more.</summary>
    public void Deliver(KeyCaptureEvent captured)
    {
        Session? session;
        lock (_lock)
        {
            session = _current;
            session?.RestartWatchdog();
        }

        session?.Invoke(captured);
    }

    /// <summary>Releases whatever is armed and tells its caller why; a no-op when nothing is.</summary>
    public void ReleaseCurrent(string reason)
    {
        Session? session;
        lock (_lock)
        {
            session = _current;
        }

        if (session is not null)
        {
            Release(session, reason, notify: true);
        }
    }

    private void Release(Session session, string reason, bool notify)
    {
        lock (_lock)
        {
            if (_current != session)
            {
                return;
            }

            _current = null;
            _gate.CaptureKeys(false);
            session.StopWatchdog();
        }

        Ended(session, reason, notify);
    }

    private void Ended(Session session, string reason, bool notify)
    {
        _log.Info(LogSources.Engine, "Key capture released", ("reason", reason));
        if (notify)
        {
            _gate.Post(WorkerMessage.Notify(() => session.Invoke(KeyCaptureEvent.Released(reason))), critical: true);
        }
    }

    /// <summary>One armed capture. Its watchdog timer is only touched under the controller's lock, so it is never changed after disposal.</summary>
    private sealed class Session(KeyCaptureController owner, Action<KeyCaptureEvent> callback, TimeSpan idleTimeout) : IDisposable
    {
        private Timer? _watchdog;

        public void Dispose() => owner.Release(this, KeyCaptureEvent.DisposedReason, notify: false);

        public void StartWatchdog() =>
            _watchdog = new Timer(_ => owner.Release(this, KeyCaptureEvent.IdleTimeoutReason, notify: true), null, idleTimeout, Timeout.InfiniteTimeSpan);

        public void RestartWatchdog() => _watchdog?.Change(idleTimeout, Timeout.InfiniteTimeSpan);

        public void StopWatchdog()
        {
            _watchdog?.Dispose();
            _watchdog = null;
        }

        public void Invoke(KeyCaptureEvent captured)
        {
            try
            {
                callback(captured);
            }
            catch (Exception exception)
            {
                owner._log.Error(LogSources.Engine, "Key capture callback threw", exception, ("kind", captured.Kind), ("key", captured.Key));
            }
        }
    }
}
