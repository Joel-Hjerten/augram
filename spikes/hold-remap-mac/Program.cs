using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

[assembly: SupportedOSPlatform("macos")]

namespace HoldRemapSpike;

/// <summary>
/// Plan 0002 step 0: Space held in Blender turns Left/Right/Middle into Middle drags (docs/plans/0002-hold-remaps.md).
/// Three threads: the hook (<see cref="HoldHook"/>, decides and records), the worker (<see cref="Worker"/>, posts and
/// logs), and this main thread, which pumps the main run loop in 100 ms slices, reads the frontmost app after each,
/// prints the countdown and runs the shutdown. Throwaway: deleted once docs/learnings/0005-hold-remaps-mac.md is written.
/// </summary>
internal static class Program
{
    public const string BlenderBundleId = "org.blenderfoundation.blender";
    private static readonly TimeSpan Slice = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan CountdownEvery = TimeSpan.FromSeconds(30);
    private static string? _stopReason;
    private static int _shutDown;
    private static SimpleGlobalHook? _hook;
    private static Thread? _hookThread;
    private static HoldHook? _logic;
    private static Worker? _worker;

    public static void RequestStop(string reason) => Interlocked.CompareExchange(ref _stopReason, reason, null);

    public static void Log(string text) => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {text}");

    private static int Main(string[] args)
    {
        if (!Options.TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(Options.Usage);
            return 2;
        }

        _worker = new Worker(options, options.NativePost ? new NativePoster() : new SharpHookPoster());
        PrintBanner(options, _worker.PosterDescription);
        if (!UioHookProvider.Instance.IsAxApiEnabled(false))
        {
            PrintPermissionHelp("macOS reports no Accessibility permission for this process.");
            return 1;
        }

        MainRunLoop.Init();
        if (Native.pthread_main_np() != 1)
        {
            Log("warning: Main is not on the main thread, so the frontmost app may never update; use --anywhere if Space is never claimed in Blender");
        }

        if (!Frontmost.Init())
        {
            Console.Error.WriteLine("NSWorkspace is not available; cannot tell which app is in front.");
            return 1;
        }

        _logic = new HoldHook(options, _worker);
        var front = Frontmost.BundleId();
        _logic.BlenderFront = front == BlenderBundleId;
        Log($"front: {front ?? "(none)"}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine($"unhandled exception: {e.ExceptionObject}");
            Shutdown("unhandled exception");
        };
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestStop("Ctrl+C");
        };
        using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnSignal);
        using var hangUp = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnSignal);

        _worker.Start();
        if (!StartHook(_logic))
        {
            return 1;
        }

        Log("hook running");
        RunUntilStop(options, front);
        Shutdown(Volatile.Read(ref _stopReason) ?? "stop");
        return 0;
    }

    private static void OnSignal(PosixSignalContext context)
    {
        context.Cancel = true;
        RequestStop(context.Signal.ToString());
    }

    private static bool StartHook(HoldHook logic)
    {
        // As Augram's SharpHookInputSource: libuiohook would otherwise resolve every key press to a character with a
        // synchronous trip to the main thread. Nothing here listens to KeyTyped.
        ((IGlobalHookProvider)UioHookProvider.Instance).KeyTypedEnabled = false;
        var hook = new SimpleGlobalHook(GlobalHookType.All);
        hook.KeyPressed += logic.OnKey;
        hook.KeyReleased += logic.OnKey;
        hook.MousePressed += logic.OnButton;
        hook.MouseReleased += logic.OnButton;
        hook.MouseMoved += logic.OnMove;
        hook.MouseDragged += logic.OnMove;
        hook.MouseWheel += logic.OnWheel;

        var enabled = new ManualResetEventSlim();
        Exception? failure = null;
        hook.HookEnabled += (_, _) => enabled.Set();
        var thread = new Thread(() =>
        {
            try
            {
                hook.Run();
            }
            catch (Exception e)
            {
                failure = e;
                enabled.Set();
            }
        })
        { IsBackground = true, Name = "spike-hook" };
        thread.Start();

        // Wait with the main run loop pumping, in case libuiohook needs the main thread while it starts.
        for (var i = 0; i < 50 && !enabled.IsSet; i++)
        {
            MainRunLoop.Run(Slice);
        }

        if (!enabled.IsSet || Volatile.Read(ref failure) is not null)
        {
            PrintPermissionHelp(failure is HookException hookFailure ? $"{hookFailure.Result}: {hookFailure.Message}" : failure?.Message ?? "it did not start within 5 s.");
            hook.Dispose();
            return false;
        }

        _hook = hook;
        _hookThread = thread;
        return true;
    }

    private static void RunUntilStop(Options options, string? lastFront)
    {
        var start = Stopwatch.GetTimestamp();
        var length = TimeSpan.FromMinutes(options.Minutes);
        var nextCountdown = CountdownEvery;
        while (Volatile.Read(ref _stopReason) is null)
        {
            MainRunLoop.Run(Slice);
            var front = Frontmost.BundleId();
            if (front != lastFront)
            {
                lastFront = front;
                _logic!.BlenderFront = front == BlenderBundleId;
                Log($"front: {front ?? "(none)"}{(front == BlenderBundleId ? " (Blender: Space is claimed)" : "")}");
            }

            var elapsed = Stopwatch.GetElapsedTime(start);
            if (elapsed >= length)
            {
                RequestStop($"{options.Minutes} min are up");
            }
            else if (elapsed >= nextCountdown)
            {
                var left = length - elapsed;
                Log($"{(int)left.TotalMinutes}:{left.Seconds:00} left (Ctrl+C quits now)");
                nextCountdown += CountdownEvery;
            }

            if (_hookThread is { IsAlive: false })
            {
                RequestStop("the hook stopped by itself (Accessibility revoked?)");
            }
        }
    }

    /// <summary>Release any held output first, then stop the hook. Idempotent; also the unhandled-exception path.</summary>
    private static void Shutdown(string why)
    {
        if (Interlocked.Exchange(ref _shutDown, 1) != 0)
        {
            return;
        }

        Log($"stopping: {why}");
        if (_logic?.Fault is { } fault)
        {
            Log($"hook handler error: {fault}");
        }

        if (_logic is not null)
        {
            _logic.Stopping = true;
        }

        if (_worker is not null && !_worker.StopAndRelease(TimeSpan.FromSeconds(2)))
        {
            Log("the worker did not answer; releasing from here");
            _worker.EmergencyRelease();
        }

        try
        {
            _hook?.Dispose();
        }
        catch (Exception e)
        {
            Log($"hook dispose: {e.Message}");
        }

        if (_hookThread is { } thread && thread != Thread.CurrentThread)
        {
            thread.Join(TimeSpan.FromSeconds(5));
        }

        Log("hook stopped");
    }

    private static void PrintBanner(Options options, string posting)
    {
        Console.WriteLine("Augram hold-remap spike (plan 0002 step 0)");
        Console.WriteLine(options.VariantB
            ? "  variant  B: while an output is held, physical drags are swallowed and re-posted as Middle drags (always CoreGraphics)"
            : "  variant  A: physical drags pass through unchanged");
        Console.WriteLine($"  posting  {posting}");
        Console.WriteLine($"  tap      Space released within {options.TapMs} ms, no mouse button or wheel used: a space is typed");
        Console.WriteLine(options.Anywhere
            ? "  where    anywhere (--anywhere): Space is claimed in every app"
            : $"  where    Blender only ({BlenderBundleId} frontmost)");
        Console.WriteLine("  remaps   Space+Left → Middle, Space+Right → Shift+Middle, Space+Middle → Ctrl+Middle, Space+Left+Right → Ctrl+Middle");
        Console.WriteLine($"  quit     Ctrl+C here; quits by itself after {options.Minutes} min");
    }

    private static void PrintPermissionHelp(string detail)
    {
        var app = Environment.GetEnvironmentVariable("TERM_PROGRAM") switch
        {
            "vscode" => "Visual Studio Code",
            "Apple_Terminal" => "Terminal",
            null => "the terminal app",
            var other => other,
        };
        Console.Error.WriteLine($"The hook could not start: {detail}");
        Console.Error.WriteLine($"macOS gives the Accessibility permission to the app the terminal runs in, here {app}.");
        Console.Error.WriteLine("System Settings > Privacy & Security > Accessibility: switch it on for that app (Terminal, or Visual Studio Code");
        Console.Error.WriteLine("for its integrated terminal), quit and reopen the app, and run this again.");
    }
}

internal sealed record Options(bool VariantB, bool NativePost, int TapMs, bool Anywhere, int Minutes)
{
    public const string Usage = "usage: HoldRemapSpike [--variant a|b] [--post sharphook|native] [--tap-ms 180] [--anywhere] [--minutes 3]";

    public static bool TryParse(string[] args, [NotNullWhen(true)] out Options? options, out string error)
    {
        var variantB = false;
        var native = false;
        var tapMs = 180;
        var anywhere = false;
        var minutes = 3;
        options = null;
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--variant" when value is "a" or "b":
                    variantB = value == "b";
                    i++;
                    break;
                case "--post" when value is "sharphook" or "native":
                    native = value == "native";
                    i++;
                    break;
                case "--tap-ms" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out tapMs) && tapMs <= 2000:
                    i++;
                    break;
                case "--minutes" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out minutes) && minutes is >= 1 and <= 30:
                    i++;
                    break;
                case "--anywhere":
                    anywhere = true;
                    break;
                default:
                    error = $"unknown or invalid argument: {args[i]}{(value is null ? "" : " " + value)}";
                    return false;
            }
        }

        options = new Options(variantB, native, tapMs, anywhere, minutes);
        error = "";
        return true;
    }
}
