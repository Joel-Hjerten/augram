// B1: can a background process bring another app's window to the foreground?
// Every 3 s, take the root owner under the cursor; if it is not already the foreground root
// and not the desktop/shell, try the three techniques in order from a worker thread.
namespace Augram.Spike2;

internal static class ActivateMode
{
    private const int PollMs = 3000;
    private const int SettleMs = 50;

    private static readonly string[] Methods =
    [
        "plain SetForegroundWindow",
        "AttachThreadInput(fg -> us) + SetForegroundWindow",
        "AttachThreadInput(fg + target -> us) + SetForegroundWindow",
        "Alt tap (SendInput) + SetForegroundWindow",
    ];

    public static int Run(CancellationToken ct)
    {
        Log.Instructions(
            "ACTIVATE (B1). Click some other window so this console is NOT the foreground window, then hover the mouse " +
            "over Chrome, over an Explorer window, and over a borderless-fullscreen game, each for a few seconds, without " +
            "clicking. Every 3 s the spike tries to activate the window under the cursor from a worker thread and logs " +
            "which technique worked (or that all failed). Watch for: does the target really come to the front, or does its " +
            "taskbar button just flash orange? Does the Alt tap open a menu bar in the target? Ctrl+C in this console to stop; " +
            "a tally per technique is printed at the end.");

        var tally = new int[Methods.Length];
        var failures = 0;
        var attempts = 0;

        var worker = new Thread(() =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var outcome = Attempt();
                    if (outcome is >= 0)
                    {
                        attempts++;
                        if (outcome < Methods.Length)
                        {
                            tally[outcome]++;
                        }
                        else
                        {
                            failures++;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Info($"attempt threw: {e.GetType().Name}: {e.Message}");
                }

                ct.WaitHandle.WaitOne(PollMs);
            }
        })
        { IsBackground = true, Name = "activate-worker" };

        worker.Start();
        worker.Join();

        Log.Info($"tally over {attempts} attempt(s): " +
                 string.Join("; ", Methods.Select((m, i) => $"{m} = {tally[i]}")) + $"; all failed = {failures}");
        return 0;
    }

    /// <summary>Returns -1 when nothing was attempted, the index of the winning method, or Methods.Length on total failure.</summary>
    private static int Attempt()
    {
        if (!Native.GetCursorPos(out var pt))
        {
            return -1;
        }
        var under = Native.WindowFromPoint(pt);
        var target = Native.RootOwner(under);
        var foreground = Native.GetForegroundWindow();
        var foregroundRoot = Native.RootOwner(foreground);

        if (target == 0)
        {
            Log.Info($"({pt.X},{pt.Y}) no window under cursor");
            return -1;
        }
        if (target == foregroundRoot)
        {
            Log.Info($"({pt.X},{pt.Y}) already foreground: {Native.Describe(target)}");
            return -1;
        }
        if (Native.IsDesktopOrShell(target))
        {
            Log.Info($"({pt.X},{pt.Y}) desktop/shell, skipped per A20: {Native.Describe(target)}");
            return -1;
        }
        if (target == Native.RootOwner(Native.GetConsoleWindow()))
        {
            Log.Info($"({pt.X},{pt.Y}) that is our own console, skipped");
            return -1;
        }

        Log.Info($"({pt.X},{pt.Y}) target {Native.Describe(target)}");
        Log.Info($"          foreground {Native.Describe(foregroundRoot)}");

        for (var i = 0; i < Methods.Length; i++)
        {
            var ok = TryMethod(i, target, foreground, out var detail);
            Log.Info($"  [{i}] {Methods[i]}: {(ok ? "ACTIVATED" : "no")}{detail}");
            if (ok)
            {
                return i;
            }
        }

        Log.Info("  all methods FAILED for " + Native.ProcessName(target));
        return Methods.Length;
    }

    private static bool TryMethod(int index, nint target, nint foreground, out string detail)
    {
        detail = "";
        var ourThread = Native.GetCurrentThreadId();
        var fgThread = Native.GetWindowThreadProcessId(foreground, out _);
        var targetThread = Native.GetWindowThreadProcessId(target, out _);
        var attachedFg = false;
        var attachedTarget = false;

        try
        {
            switch (index)
            {
                case 1:
                    attachedFg = fgThread != 0 && fgThread != ourThread && Native.AttachThreadInput(fgThread, ourThread, true);
                    if (!attachedFg)
                    {
                        detail += " (AttachThreadInput to fg thread failed)";
                    }
                    break;
                case 2:
                    attachedFg = fgThread != 0 && fgThread != ourThread && Native.AttachThreadInput(fgThread, ourThread, true);
                    attachedTarget = targetThread != 0 && targetThread != ourThread && targetThread != fgThread
                                     && Native.AttachThreadInput(targetThread, ourThread, true);
                    if (!attachedFg || !attachedTarget)
                    {
                        detail += $" (attach fg={attachedFg} target={attachedTarget})";
                    }
                    break;
                case 3:
                    Native.SendAltTap();
                    break;
            }

            var result = Native.SetForegroundWindow(target);
            var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (!result)
            {
                detail += $" (SetForegroundWindow returned false, error {error})";
            }
        }
        finally
        {
            if (attachedFg)
            {
                Native.AttachThreadInput(fgThread, ourThread, false);
            }
            if (attachedTarget)
            {
                Native.AttachThreadInput(targetThread, ourThread, false);
            }
        }

        Thread.Sleep(SettleMs);
        var now = Native.RootOwner(Native.GetForegroundWindow());
        if (now == target)
        {
            return true;
        }
        detail += $" -> foreground now {Native.ProcessName(now)}";
        return false;
    }
}
