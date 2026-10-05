// B4: keyboard. (a) can a SharpHook keyboard hook suppress everything, including the Win key
// and Win+L, for the hotkey-capture field; (b) can SharpHook send media keys; (c) can it type
// text into Notepad and into a game console, both as Unicode text entry and as key presses.
using System.Diagnostics;
using Microsoft.Win32;
using SharpHook;
using SharpHook.Data;

namespace Augram.Spike2;

internal static class KeysMode
{
    private const int SuppressWindowMs = 10_000;
    private const string SampleText = "augram test 123";

    private static volatile bool _suppress;
    private static bool _sawWinL;
    private static bool _locked;
    private static readonly HashSet<KeyCode> HeldModifiers = [];

    public static int Run(string[] args, CancellationToken ct)
    {
        // Optional second argument selects parts, e.g. `keys bc` skips the suppression window.
        var parts = args.Length > 1 ? args[1].ToLowerInvariant() : "abc";
        Log.Instructions(
            "KEYS (B4). Part (a): for 10 seconds after the countdown EVERY key event is suppressed system-wide and printed here. " +
            "Press combinations you want a hotkey field to capture: Win alone, Win+L, Win+E, Esc, PrintScreen, Ctrl+Shift+S, " +
            "Alt+F4, media keys. Watch for: Start menu opening, Explorer launching, a screenshot flash, and above all the " +
            "machine LOCKING on Win+L (that means suppression failed). Ctrl+C does not work during those 10 s; a watchdog " +
            "releases suppression automatically. Part (b) then sends VolumeUp x2, VolumeDown x2, PlayPause via SharpHook; " +
            "watch the volume OSD. Part (c) types '" + SampleText + "' twice after 5 s countdowns: once via SimulateTextEntry, " +
            "once as individual key presses. Before each countdown click into Notepad (first run) or a game console (second run).");

        SystemEvents.SessionSwitch += (_, e) =>
        {
            Log.Info($"SESSION SWITCH: {e.Reason}");
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                _locked = true;
            }
        };

        using var hook = new SimpleGlobalHook(GlobalHookType.Keyboard);
        hook.KeyPressed += OnKey;
        hook.KeyReleased += OnKey;
        hook.KeyTyped += (_, e) =>
        {
            if (e.IsEventSimulated)
            {
                Log.Info($"  typed (simulated): '{e.Data.KeyChar}'");
            }
        };
        var hookThread = new Thread(() =>
        {
            try
            { hook.Run(); }
            catch (HookException e) { Log.Info($"keyboard hook failed: {e.Result} {e.Message}"); }
        })
        { IsBackground = true, Name = "keyboard-hook" };
        hookThread.Start();

        try
        {
            if (parts.Contains('a'))
            {
                PartA(ct);
            }
            if (parts.Contains('b') && !ct.IsCancellationRequested)
            {
                PartB(ct);
            }
            if (parts.Contains('c') && !ct.IsCancellationRequested)
            {
                PartC(ct);
            }
        }
        finally
        {
            _suppress = false;
            hook.Dispose();
            hookThread.Join(3000);
            Log.Info("keyboard hook disposed, suppression off");
        }

        Log.Info("keys mode finished; press Ctrl+C or close the window");
        ct.WaitHandle.WaitOne();
        return 0;
    }

    private static void PartA(CancellationToken ct)
    {
        Countdown("(a) suppression starts", 5, ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        _suppress = true;
        var started = Stopwatch.StartNew();
        Log.Info($"(a) SUPPRESSING ALL KEYS for {SuppressWindowMs / 1000} s. Press your combinations now.");
        ct.WaitHandle.WaitOne(SuppressWindowMs);
        _suppress = false;
        Log.Info($"(a) watchdog released suppression after {started.ElapsedMilliseconds} ms");

        lock (HeldModifiers)
        {
            if (HeldModifiers.Count > 0)
            {
                Log.Info($"(a) note: modifiers still physically held at release time: {string.Join("+", HeldModifiers)} " +
                         "(their downs were suppressed, so the OS never saw them; release them now)");
            }
            HeldModifiers.Clear();
        }

        Thread.Sleep(1500); // give a lock, if any, time to be reported
        Log.Info(_sawWinL
            ? (_locked ? "(a) RESULT: Win+L was seen AND the session locked -> suppression FAILED for Win+L"
                       : "(a) RESULT: Win+L was seen and the session did NOT lock -> Win+L suppression works")
            : "(a) RESULT: Win+L was not pressed during the window (no verdict on Win+L)");
    }

    private static void OnKey(object? sender, KeyboardHookEventArgs e)
    {
        var isDown = e.RawEvent.Type == EventType.KeyPressed;
        var key = e.Data.KeyCode;

        if (e.IsEventSimulated)
        {
            Log.Info($"  {(isDown ? "down" : "up  ")} (simulated): {key} raw=0x{e.Data.RawCode:X}");
            return;
        }
        if (!_suppress)
        {
            return;
        }

        e.SuppressEvent = true;

        string combo;
        lock (HeldModifiers)
        {
            if (IsModifier(key))
            {
                if (isDown)
                {
                    HeldModifiers.Add(key);
                }
                else
                {
                    HeldModifiers.Remove(key);
                }
            }
            combo = string.Join("+", HeldModifiers.Select(ModifierName).Distinct().Append(IsModifier(key) ? "" : key.ToString()).Where(s => s.Length > 0));
        }

        Log.Info($"  SUPPRESSED {(isDown ? "down" : "up  ")}: {combo} ({key} raw=0x{e.Data.RawCode:X} mask={e.RawEvent.Mask})");

        if (isDown && key == KeyCode.VcL && e.RawEvent.Mask.HasFlag(EventMask.Meta))
        {
            _sawWinL = true;
            Log.Info("  -> that was Win+L; if the machine locks now, suppression failed");
        }
    }

    private static bool IsModifier(KeyCode k) => k is KeyCode.VcLeftShift or KeyCode.VcRightShift or KeyCode.VcLeftControl
        or KeyCode.VcRightControl or KeyCode.VcLeftAlt or KeyCode.VcRightAlt or KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    private static string ModifierName(KeyCode k) => k switch
    {
        KeyCode.VcLeftShift or KeyCode.VcRightShift => "Shift",
        KeyCode.VcLeftControl or KeyCode.VcRightControl => "Ctrl",
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt => "Alt",
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta => "Win",
        _ => k.ToString(),
    };

    private static void PartB(CancellationToken ct)
    {
        Countdown("(b) media keys", 3, ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }
        var sim = new EventSimulator();
        Tap(sim, KeyCode.VcVolumeUp);
        Tap(sim, KeyCode.VcVolumeUp);
        Thread.Sleep(300);
        Tap(sim, KeyCode.VcVolumeDown);
        Tap(sim, KeyCode.VcVolumeDown);
        Thread.Sleep(300);
        Tap(sim, KeyCode.VcMediaPlay);
        Log.Info("(b) media keys sent; did the volume OSD show +2 then -2, and did your player toggle play/pause?");
    }

    private static void Tap(EventSimulator sim, KeyCode key)
    {
        var down = sim.SimulateKeyPress(key);
        var up = sim.SimulateKeyRelease(key);
        Log.Info($"  simulate {key}: press={down} release={up}");
        Thread.Sleep(80);
    }

    private static void PartC(CancellationToken ct)
    {
        var sim = new EventSimulator();

        Countdown("(c1) SimulateTextEntry into the focused window", 5, ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }
        Log.Info($"(c1) typing into {Native.Describe(Native.GetForegroundWindow())}");
        var sw = Stopwatch.StartNew();
        var r1 = sim.SimulateTextEntry(SampleText);
        Log.Info($"(c1) SimulateTextEntry(\"{SampleText}\") -> {r1} in {sw.ElapsedMilliseconds} ms");

        Countdown("(c2) individual key presses into the focused window", 5, ct);
        if (ct.IsCancellationRequested)
        {
            return;
        }
        Log.Info($"(c2) typing into {Native.Describe(Native.GetForegroundWindow())}");
        sw.Restart();
        var failures = 0;
        foreach (var c in SampleText)
        {
            var key = c == ' ' ? KeyCode.VcSpace : Enum.Parse<KeyCode>("Vc" + char.ToUpperInvariant(c));
            if (sim.SimulateKeyPress(key) != UioHookResult.Success)
            {
                failures++;
            }
            if (sim.SimulateKeyRelease(key) != UioHookResult.Success)
            {
                failures++;
            }
            Thread.Sleep(10);
        }
        Log.Info($"(c2) {SampleText.Length} key presses sent in {sw.ElapsedMilliseconds} ms, failures={failures}. " +
                 "Compare the two lines in the target: both should read exactly '" + SampleText + "'");
    }

    private static void Countdown(string what, int seconds, CancellationToken ct)
    {
        for (var i = seconds; i > 0 && !ct.IsCancellationRequested; i--)
        {
            Log.Info($"{what} in {i}...");
            ct.WaitHandle.WaitOne(1000);
        }
    }
}
