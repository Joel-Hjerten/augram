namespace Augram.Core.Diagnostics;

/// <summary>
/// Every <see cref="LogEvent.Source"/> the solution emits: short lowercase component names the
/// Diagnostics › Log tab filters on and an agent greps for. A component that logs uses one of these;
/// one that needs a new source adds a constant here and to <see cref="All"/>, never a literal at the call site.
/// </summary>
public static class LogSources
{
    /// <summary>The App's own lifecycle: started, stopping, another launch arriving, the launch window, the tray icon.</summary>
    public const string App = "app";

    /// <summary>Strokes, clicks replayed, wheel triggers, cancellations, queue health.</summary>
    public const string Capture = "capture";

    /// <summary>The configuration file: loaded, created, what loading it noticed (a recovery, a reset, skipped entries), imported steps upgraded.</summary>
    public const string Config = "config";

    /// <summary>Engine lifecycle: start, stop, enabled toggles, setting changes, key capture, the Accessibility permission.</summary>
    public const string Engine = "engine";

    /// <summary>Command execution: what a trigger resolved to, activation, each step, the command's outcome.</summary>
    public const string Execution = "exec";

    /// <summary>Augram files written by Export (F8).</summary>
    public const string Export = "export";

    /// <summary>Hold remaps (F9): whether the foreground is watched for them, each hold and its tap, rollovers, outputs pressed and released, failed injections.</summary>
    public const string Hold = "hold";

    /// <summary>Hook install, loss, reinstall, system events, per-event traces.</summary>
    public const string Hook = "hook";

    /// <summary>The ignore list's watch: what it watches, the pointer entering and leaving an ignored app, pauses while a "disable while focused" app has focus.</summary>
    public const string Ignore = "ignore";

    /// <summary>Imports: Augram files (F8) and StrokesPlus.net.</summary>
    public const string Import = "import";

    /// <summary>The log's own reports: events dropped because its queue was full.</summary>
    public const string Log = "log";

    /// <summary>The trail overlay: its area, style verified (click-through), first frame, the watchdog hiding it.</summary>
    public const string Overlay = "overlay";

    /// <summary>Per-stroke recognition result: top matches with scores, why nothing fired.</summary>
    public const string Recognition = "recognition";

    /// <summary>Start at login: checked, registered, removed, changed outside Augram.</summary>
    public const string Startup = "startup";

    /// <summary>One line per step execution, and what a platform adapter reports about running one.</summary>
    public const string Steps = "steps";

    /// <summary>Sync (F8): one line per run with counts only, folder and repository changes, failures.</summary>
    public const string Sync = "sync";

    /// <summary>Training (Gestures › draw area): where a stroke started relative to the draw area.</summary>
    public const string Training = "training";

    /// <summary>Window operations: activation attempts and their technique, macOS Window menu presses and restores.</summary>
    public const string Window = "window";

    /// <summary>Every source above, in alphabetical order; the Diagnostics › Log source filter lists these.</summary>
    public static IReadOnlyList<string> All { get; } =
        [App, Capture, Config, Engine, Execution, Export, Hold, Hook, Ignore, Import, Log, Overlay, Recognition, Startup, Steps, Sync, Training, Window];
}
