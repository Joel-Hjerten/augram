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

    /// <summary>The press was a plain click. Inject a clean down+up pair at this position (A19; only the worker injects).</summary>
    public sealed record ReplayClick(MouseButton Button, int X, int Y) : CaptureOutcome;

    /// <summary>
    /// A stroke was released. <paramref name="Points"/> starts with the press point and ends with the
    /// release point; the list is handed over, the machine never touches it again. Recognition runs
    /// on this, never earlier (CLAUDE.md invariant 4).
    /// </summary>
    public sealed record StrokeComplete(IReadOnlyList<CapturePoint> Points, CapturePoint Start, MouseButton Button) : CaptureOutcome;

    /// <summary>A wheel tick while held. Fires once per tick, at the window under <paramref name="Start"/>.</summary>
    public sealed record WheelTrigger(WheelDirection Direction, CapturePoint Start) : CaptureOutcome;

    /// <summary>The gesture was cancelled. Nothing fires, nothing is replayed; the release will still be consumed.</summary>
    public sealed record Cancelled(CancelReason Reason) : CaptureOutcome;
}
