// B3: does the SharpHook mouse hook survive sleep, lock, unlock, and hours of uptime?
// Logs event counts once a minute, session/power events as they happen, and runs a watchdog:
// if the cursor is visibly moving (GetCursorPos polled every 500 ms) but no hook event has
// arrived for 15 s, the hook is declared dead and reinstalled.
using System.Diagnostics;
using Microsoft.Win32;
using SharpHook;
using SharpHook.Data;

namespace Augram.Spike2;

internal static class HookMode
{
    private const int DeadAfterMs = 15_000;
    private const int PollMs = 500;
    private const int ReportMs = 60_000;

    private static readonly object Gate = new();
    private static SimpleGlobalHook? _hook;
    private static Thread? _hookThread;
    private static int _generation;

    private static long _lastEventTicks = Stopwatch.GetTimestamp();
    private static long _reinstalledAtTicks;
    private static bool _awaitingProofOfLife;
    private static readonly long[] Counts = new long[Enum.GetValues<EventType>().Length];
    private static long _total;

    public static int Run(CancellationToken ct)
    {
        Log.Instructions(
            "HOOK (B3). Leave this running for hours. Put the machine to sleep and wake it, lock it with Win+L and unlock, " +
            "change display settings, let the screen saver kick in. After each event, move the mouse around for a few seconds. " +
            "One line per minute shows event counts; session switch and power mode changes are logged as they happen. " +
            "If no mouse events arrive for 15 s while the cursor is moving, the spike logs HOOK DEAD, reinstalls the hook, " +
            "and logs whether events flow again. Ctrl+C to stop. The log file is next to the exe.");

        SystemEvents.SessionSwitch += (_, e) => Log.Info($"SESSION SWITCH: {e.Reason}");
        SystemEvents.PowerModeChanged += (_, e) => Log.Info($"POWER MODE: {e.Mode}");
        SystemEvents.DisplaySettingsChanged += (_, _) => Log.Info("DISPLAY SETTINGS CHANGED");
        SystemEvents.SessionEnding += (_, e) => Log.Info($"SESSION ENDING: {e.Reason}");

        Install("initial");

        var watchdog = new Thread(() => Watchdog(ct)) { IsBackground = true, Name = "hook-watchdog" };
        watchdog.Start();
        watchdog.Join();

        lock (Gate)
        {
            _hook?.Dispose();
            _hook = null;
        }
        Log.Info($"hook disposed; total events {_total}, reinstalls {_generation - 1}");
        return 0;
    }

    private static void Install(string reason)
    {
        lock (Gate)
        {
            _generation++;
            var generation = _generation;
            var hook = new SimpleGlobalHook(GlobalHookType.Mouse);
            hook.HookEnabled += (_, _) => Log.Info($"[gen {generation}] HookEnabled");
            hook.HookDisabled += (_, _) => Log.Info($"[gen {generation}] HookDisabled");
            hook.MouseMoved += OnEvent;
            hook.MouseDragged += OnEvent;
            hook.MousePressed += OnEvent;
            hook.MouseReleased += OnEvent;
            hook.MouseClicked += OnEvent;
            hook.MouseWheel += OnWheel;
            _hook = hook;
            _reinstalledAtTicks = Stopwatch.GetTimestamp();
            _awaitingProofOfLife = true;

            _hookThread = new Thread(() =>
            {
                try
                {
                    hook.Run(); // blocks until Dispose/Stop
                    Log.Info($"[gen {generation}] hook.Run returned normally");
                }
                catch (HookException e)
                {
                    Log.Info($"[gen {generation}] hook.Run threw HookException: {e.Result} {e.Message}");
                }
                catch (Exception e)
                {
                    Log.Info($"[gen {generation}] hook.Run threw {e.GetType().Name}: {e.Message}");
                }
            })
            { IsBackground = true, Name = $"hook-gen{generation}" };
            _hookThread.Start();
            Log.Info($"[gen {generation}] hook installed ({reason}) on thread {_hookThread.ManagedThreadId}");
        }
    }

    private static void OnEvent(object? sender, MouseHookEventArgs e) => Count(e.RawEvent.Type);
    private static void OnWheel(object? sender, MouseWheelHookEventArgs e) => Count(e.RawEvent.Type);

    private static void Count(EventType type)
    {
        // Hook thread: increment and return. Logging only on the rare proof-of-life transition.
        Interlocked.Increment(ref Counts[(int)type]);
        Interlocked.Increment(ref _total);
        var now = Stopwatch.GetTimestamp();
        Volatile.Write(ref _lastEventTicks, now);
        if (Volatile.Read(ref _awaitingProofOfLife))
        {
            Volatile.Write(ref _awaitingProofOfLife, false);
            Log.Info($"[gen {_generation}] alive: first event {Ms(now - _reinstalledAtTicks):F0} ms after install");
        }
    }

    private static void Watchdog(CancellationToken ct)
    {
        Native.GetCursorPos(out var last);
        var movesSinceLastEvent = 0;
        var lastSeenEventTicks = Volatile.Read(ref _lastEventTicks);
        var lastReport = Stopwatch.GetTimestamp();
        var lastCounts = new long[Counts.Length];
        var deadSince = 0L;

        while (!ct.WaitHandle.WaitOne(PollMs))
        {
            var now = Stopwatch.GetTimestamp();
            var lastEvent = Volatile.Read(ref _lastEventTicks);
            if (lastEvent != lastSeenEventTicks)
            {
                lastSeenEventTicks = lastEvent;
                movesSinceLastEvent = 0;
                if (deadSince != 0)
                {
                    Log.Info($"events flowing again after {Ms(now - deadSince) / 1000:F0} s of silence");
                    deadSince = 0;
                }
            }

            if (Native.GetCursorPos(out var cur) && (cur.X != last.X || cur.Y != last.Y))
            {
                last = cur;
                movesSinceLastEvent++;
            }

            var silentMs = Ms(now - lastEvent);
            if (silentMs > DeadAfterMs && movesSinceLastEvent >= 4)
            {
                Log.Info($"HOOK DEAD: cursor moved in {movesSinceLastEvent} polls but no hook event for {silentMs / 1000:F0} s " +
                         $"(hook thread alive={_hookThread?.IsAlive}, IsRunning={_hook?.IsRunning})");
                deadSince = now;
                Reinstall();
                movesSinceLastEvent = 0;
                Volatile.Write(ref _lastEventTicks, Stopwatch.GetTimestamp()); // restart the 15 s clock
                lastSeenEventTicks = Volatile.Read(ref _lastEventTicks);
            }

            if (Ms(now - lastReport) >= ReportMs)
            {
                lastReport = now;
                var parts = new List<string>();
                for (var i = 0; i < Counts.Length; i++)
                {
                    var c = Volatile.Read(ref Counts[i]);
                    var delta = c - lastCounts[i];
                    lastCounts[i] = c;
                    if (delta > 0)
                    {
                        parts.Add($"{(EventType)i}={delta}");
                    }
                }
                Log.Info($"[gen {_generation}] last minute: {(parts.Count == 0 ? "no events" : string.Join(" ", parts))}; " +
                         $"total {Volatile.Read(ref _total)}; last event {silentMs / 1000:F0} s ago; hook thread alive={_hookThread?.IsAlive}");
            }
        }
    }

    private static void Reinstall()
    {
        SimpleGlobalHook? old;
        Thread? oldThread;
        lock (Gate)
        {
            old = _hook;
            oldThread = _hookThread;
        }
        var sw = Stopwatch.StartNew();
        try
        {
            old?.Dispose();
        }
        catch (Exception e)
        {
            Log.Info($"dispose of old hook threw {e.GetType().Name}: {e.Message}");
        }
        var joined = oldThread?.Join(5000) ?? true;
        Log.Info($"old hook disposed in {sw.ElapsedMilliseconds} ms, old thread exited={joined}");
        Install("reinstall after HOOK DEAD");
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
