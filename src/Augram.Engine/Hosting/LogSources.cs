namespace Augram.Engine.Hosting;

/// <summary>
/// The Engine's log source names (Core/Diagnostics README contract: short lowercase component names
/// the Diagnostics tab filters on). <see cref="Recognition"/> is the name that README already lists.
/// </summary>
public static class LogSources
{
    /// <summary>Hook install, loss, reinstall, system events, per-event traces.</summary>
    public const string Hook = "hook";

    /// <summary>Strokes, clicks replayed, wheel triggers, cancellations, queue health.</summary>
    public const string Capture = "capture";

    /// <summary>Per-stroke recognition result: top matches with scores, why nothing fired.</summary>
    public const string Recognition = "recognition";

    /// <summary>Engine lifecycle: start, stop, enabled toggles, setting changes.</summary>
    public const string Engine = "engine";

    /// <summary>Command execution: what a trigger resolved to, activation, each step, the command's outcome.</summary>
    public const string Execution = "exec";

    /// <summary>The ignore list's watch: what it watches, the pointer entering and leaving an ignored app, pauses while a "disable while focused" app has focus.</summary>
    public const string Ignore = "ignore";
}
