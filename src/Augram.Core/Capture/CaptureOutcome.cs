using Augram.Core.Abstractions;

namespace Augram.Core.Capture;

/// <summary>
/// What the <see cref="CaptureStateMachine"/> wants done after one event. A closed set.
/// <see cref="Suppress"/> and <see cref="PassThrough"/> are the input decision the hook handler
/// must apply synchronously; every other outcome is work for the Engine worker (overlay, replay,
/// recognition, wheel command, log).
/// </summary>
public abstract record CaptureOutcome
{
    private protected CaptureOutcome()
    {
    }

    /// <summary>Consume the event: the OS and the app under the pointer never see it.</summary>
    public sealed record Suppress : CaptureOutcome
    {
        public static Suppress Instance { get; } = new();
    }

    /// <summary>Let the event through untouched.</summary>
    public sealed record PassThrough : CaptureOutcome
    {
        public static PassThrough Instance { get; } = new();
    }

    /// <summary>The hold became a stroke. Show the trail, starting at the press point.</summary>
    public sealed record BeginStroke(CapturePoint Start) : CaptureOutcome;

    /// <summary>A point was recorded. Extend the trail.</summary>
    public sealed record StrokeProgress(CapturePoint Point) : CaptureOutcome;

    /// <summary>The trail is over, whatever happens next (completion, cancel, or wheel takeover).</summary>
    public sealed record EndStroke : CaptureOutcome
    {
        public static EndStroke Instance { get; } = new();
    }

    /// <summary>
    /// The press was a plain click. Inject a clean down+up pair at this position (A19; only the worker injects).
    /// <see cref="AfterKeys"/> are keys pressed during the press and swallowed: they are pressed around the click so the
    /// app gets the click with them held (Joel, 2026-10-09: an unbound click with keys passes through with them).
    /// </summary>
    public sealed record ReplayClick(MouseButton Button, int X, int Y) : CaptureOutcome
    {
        public KeyModifiers AfterKeys { get; init; }
    }

    /// <summary>
    /// A stroke-button click with keys or buttons held (SP.net's no-gesture action): resolve the click trigger for
    /// <paramref name="Hold"/> over the window under <paramref name="Start"/>. When nothing fires there, the click is relayed
    /// at (<paramref name="X"/>, <paramref name="Y"/>) with the After keys around it, unless an After button took part.
    /// </summary>
    public sealed record ClickTrigger(MouseButton Button, int X, int Y, CapturePoint Start, PressHold Hold) : CaptureOutcome;

    /// <summary>
    /// A stroke was released. <paramref name="Points"/> starts with the press point and ends with the
    /// release point; the list is handed over, the machine never touches it again. Recognition runs
    /// on this, never earlier (CLAUDE.md invariant 4). <see cref="Hold"/> is what the press held.
    /// </summary>
    public sealed record StrokeComplete(IReadOnlyList<CapturePoint> Points, CapturePoint Start, MouseButton Button) : CaptureOutcome
    {
        public PressHold Hold { get; init; }
    }

    /// <summary>
    /// A wheel tick while held. Fires once per tick, at the window under <paramref name="Start"/>. <see cref="Hold"/> is the
    /// press's set, frozen at the first tick; <see cref="AfterDrawing"/> says the stroke had passed the start distance before
    /// the first tick, so the tick matches a drawn gesture + wheel, which no command is, never a wheel command (SP.net,
    /// learnings 0003 §2.4).
    /// </summary>
    public sealed record WheelTrigger(WheelDirection Direction, CapturePoint Start) : CaptureOutcome
    {
        public PressHold Hold { get; init; }

        public bool AfterDrawing { get; init; }
    }

    /// <summary>
    /// A held-back anchor (not the stroke button) goes back to the app: inject its down at <paramref name="Start"/>, then put
    /// the pointer back at (<paramref name="X"/>, <paramref name="Y"/>), so a drag or a long press starts where it began.
    /// </summary>
    public sealed record HandBack(MouseButton Button, CapturePoint Start, int X, int Y) : CaptureOutcome;

    /// <summary>The physical release of a handed-back anchor was consumed: inject its up here, pairing the injected down.</summary>
    public sealed record ReleaseHandedBack(MouseButton Button, int X, int Y) : CaptureOutcome;

    /// <summary>The gesture was cancelled. Nothing fires, nothing is replayed; the release will still be consumed.</summary>
    public sealed record Cancelled(CancelReason Reason) : CaptureOutcome;
}
