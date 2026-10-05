using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Platform.Windows.WindowSystem;

/// <summary>
/// B1: brings another process's window to the foreground from a background process. Windows refuses a plain
/// <c>SetForegroundWindow</c> unless the caller owns the input or was the last to receive it, so three techniques
/// run in order, each verified by polling the foreground root: plain call; <c>AttachThreadInput</c> to the current
/// foreground thread (StrokeIt, GestureSign); an Alt tap through <c>SendInput</c>, which counts as "last input"
/// and lifts the lock. Runs on the engine worker; it sleeps, so it must never run on the hook thread.
/// </summary>
internal sealed class ForegroundActivator
{
    public const string Source = "window";
    private const int SettleMs = 50;
    private const int PollMs = 5;
    private static readonly string[] Techniques = ["set-foreground", "attach-thread-input", "alt-tap"];

    private readonly IWin32Foreground _win;
    private readonly IWin32Windows _query;
    private readonly IEventLog _log;
    private readonly Action<int> _sleep;

    public ForegroundActivator(IWin32Foreground win, IWin32Windows query, IEventLog log, Action<int>? sleep = null)
    {
        _win = win;
        _query = query;
        _log = log;
        _sleep = sleep ?? Thread.Sleep;
    }

    public ActivationResult Activate(WindowIdentity target)
    {
        var clock = Stopwatch.StartNew();
        var foreground = _win.ForegroundWindow();

        for (var i = 0; i < Techniques.Length; i++)
        {
            var technique = Techniques[i];
            Attempt(i, target.RootHandle, foreground);
            if (SettledOn(target.RootHandle))
            {
                var elapsed = (int)clock.ElapsedMilliseconds;
                _log.Info(Source, "Window activated", ("technique", technique), ("elapsedMs", elapsed), ("process", target.ProcessName));
                return new ActivationResult(true, technique, elapsed);
            }

            _log.Info(Source, "Activation attempt failed", ("technique", technique), ("elapsedMs", (int)clock.ElapsedMilliseconds), ("process", target.ProcessName));
        }

        var total = (int)clock.ElapsedMilliseconds;
        _log.Warning(Source, "Activation failed", ("technique", "none"), ("elapsedMs", total), ("process", target.ProcessName));
        return new ActivationResult(false, "none", total);
    }

    private void Attempt(int index, nint target, nint foreground)
    {
        switch (index)
        {
            case 0:
                _win.SetForegroundWindow(target);
                break;
            case 1:
                WithAttachedInput(foreground, () => _win.SetForegroundWindow(target));
                break;
            default:
                _win.SendAltTap();
                _win.SetForegroundWindow(target);
                break;
        }
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

    private bool SettledOn(nint target)
    {
        for (var waited = 0; ; waited += PollMs)
        {
            if (Root(_win.ForegroundWindow()) == target)
            {
                return true;
            }

            if (waited >= SettleMs)
            {
                return false;
            }

            _sleep(PollMs);
        }
    }

    private nint Root(nint hwnd)
    {
        if (hwnd == 0)
        {
            return 0;
        }

        var root = _query.RootOwner(hwnd);
        return root == 0 ? hwnd : root;
    }
}
