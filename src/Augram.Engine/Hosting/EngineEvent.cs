using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;

namespace Augram.Engine.Hosting;

/// <summary>
/// What the engine tells the App after a stroke or a wheel tick (M1: nothing executes yet; M2's
/// command resolution hangs off these). Raised on the engine worker thread; the App marshals to
/// its UI. The raw stroke points travel along so training can store them as a sample (F3).
/// </summary>
public abstract record EngineEvent
{
    private protected EngineEvent()
    {
    }

    /// <summary>The best active gesture scored above the threshold.</summary>
    public sealed record GestureRecognized(
        GestureId GestureId,
        string Name,
        double Score,
        CapturePoint Start,
        IReadOnlyList<CapturePoint> Points,
        IReadOnlyList<RecognitionCandidate> TopMatches) : EngineEvent;

    /// <summary>A stroke was completed but no gesture scored above the threshold. <paramref name="Reason"/> is the log's wording.</summary>
    public sealed record NoMatch(
        string Reason,
        CapturePoint Start,
        IReadOnlyList<CapturePoint> Points,
        IReadOnlyList<RecognitionCandidate> TopMatches) : EngineEvent;

    /// <summary>A wheel tick while the stroke button was held; one per tick.</summary>
    public sealed record WheelTriggered(WheelDirection Direction, CapturePoint Start) : EngineEvent;
}
