using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// B1: brings another process's window to the foreground from a background process. Windows refuses a plain
/// <c>SetForegroundWindow</c> unless the caller owns the input or was the last to receive it, so three techniques
/// run in order, each verified by polling the foreground root: plain call; <c>AttachThreadInput</c> to the current
/// foreground thread (StrokeIt, GestureSign); an Alt tap through <c>SendInput</c>, which counts as "last input"
/// and lifts the lock. Runs on the engine worker; it sleeps, so it must never run on the hook thread. Logs one line per
/// technique that did not settle (the B1 tally); the outcome itself ("Window activated" / "Activation failed") is logged once,
/// by the engine's command runner (<c>exec</c>, with <c>focusMoved</c>), on both platforms (plan 0007 Q7).
/// </summary>
internal sealed class ForegroundActivator
{
    private const int SettleMs = 50;
    private const int PollMs = 5;
    private static readonly string[] Techniques = ["set-foreground", "attach-thread-input", "alt-tap"];

    private readonly IWin32Foreground _win;
    private readonly IWin32Windows _query;
    private readonly IEventLog _log;
    private readonly Action<int> _sleep;
    private readonly TimeProvider _time;

    public ForegroundActivator(IWin32Foreground win, IWin32Windows query, IEventLog log, Action<int>? sleep = null, TimeProvider? time = null)
    {
        _win = win;
        _query = query;
        _log = log;
        _sleep = sleep ?? Thread.Sleep;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Brings the target's root owner forward: the Open app step's case, where the window found is an unowned top-level one.</summary>
    public ActivationResult Activate(WindowIdentity target) => Activate(target, target.RootHandle);

    /// <summary>
    /// Brings <paramref name="window"/>, a top-level window of <paramref name="target"/>, forward. Settled means that window
    /// itself is in front (<c>GA_ROOT</c> of the foreground), not merely one it owns: that is the blind spot plan 0008 closed.
    /// The one exception is a disabled window whose modal dialog is in front, which is as far forward as it can come.
    /// </summary>
    public ActivationResult Activate(WindowIdentity target, nint window)
    {
        var started = _time.GetTimestamp();
        var foreground = _win.ForegroundWindow();

        for (var i = 0; i < Techniques.Length; i++)
        {
            var technique = Techniques[i];
            var (accepted, error) = Attempt(i, window, foreground);
            if (SettledOn(window))
            {
                return new ActivationResult(true, technique, ElapsedMs(started));
            }

            // What Windows answered and what stayed in front: plan 0008's open question (the same call succeeds from a plain console process).
            _log.Info(
                LogSources.Window,
                "Activation attempt failed",
                ("technique", technique),
                ("elapsedMs", ElapsedMs(started)),
                ("accepted", accepted),
                ("error", error),
                ("front", _query.ClassName(_win.ForegroundWindow())),
                ("process", target.ProcessName));
        }

        return new ActivationResult(false, "none", ElapsedMs(started));
    }

    /// <summary>One technique; returns <c>SetForegroundWindow</c>'s answer and the error code read right after it.</summary>
    private (bool Accepted, int Error) Attempt(int index, nint target, nint foreground)
    {
        (bool, int) result = default;
        switch (index)
        {
            case 0:
                result = Call(target);
                break;
            case 1:
                WithAttachedInput(foreground, () => result = Call(target));
                break;
            default:
                _win.SendAltTap();
                result = Call(target);
                break;
        }

        return result;
    }

    private (bool Accepted, int Error) Call(nint target)
    {
        var accepted = _win.SetForegroundWindow(target);
        return (accepted, accepted ? 0 : _win.LastError());
    }

    private void WithAttachedInput(nint foreground, Action call)
    {
        var ours = _win.CurrentThreadId();
        var theirs = _win.WindowThreadId(foreground);
        var attached = theirs != 0 && theirs != ours && _win.AttachThreadInput(theirs, ours, true);
        try
        {
            call();
        }
        finally
        {
            if (attached)
            {
                _win.AttachThreadInput(theirs, ours, false);
            }
        }
    }

    /// <summary>
    /// Polls until the window is in front or the settle budget has passed <b>by the clock</b>: a Windows sleep rounds up to
    /// the 15.6 ms timer tick, so counting ten 5 ms sleeps as 50 ms ran 153 ms per technique (plan 0008, measured 2026-10-12).
    /// </summary>
    private bool SettledOn(nint window)
    {
        var started = _time.GetTimestamp();
        while (true)
        {
            var foreground = _win.ForegroundWindow();
            if (_query.RootOrSelf(foreground) == window || (!_query.IsEnabled(window) && _query.RootOwnerOrSelf(foreground) == window))
            {
                return true;
            }

            // Stop when the next poll would land past the budget: with 16 ms ticks that is 48 ms, not 62.
            if (ElapsedMs(started) + PollMs > SettleMs)
            {
                return false;
            }

            _sleep(PollMs);
        }
    }

    private int ElapsedMs(long started) => (int)_time.GetElapsedTime(started).TotalMilliseconds;
}
