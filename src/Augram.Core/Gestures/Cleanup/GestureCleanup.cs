namespace Augram.Core.Gestures.Cleanup;

/// <summary>
/// Shape cleanup applied to a whole gesture (plan 0001 M2 step 10): <see cref="CleanUp"/> replaces each sample with its
/// cleaned shape and keeps the drawn samples in <see cref="Gesture.OriginalSamples"/>; <see cref="Restore"/> puts them back.
/// A gesture already cleaned is cleaned again from its originals, never from a cleaned shape. Pure: the caller stores the
/// result through the library (one undo step).
/// </summary>
public static class GestureCleanup
{
    public static Gesture CleanUp(Gesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        var drawn = gesture.OriginalSamples ?? gesture.Samples;
        return gesture with { Samples = [.. drawn.Select(Clean)], OriginalSamples = drawn };
    }

    public static Gesture Restore(Gesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        return gesture.OriginalSamples is { } drawn ? gesture with { Samples = drawn, OriginalSamples = null } : gesture;
    }

    /// <summary>A gesture stored from one drawn stroke: cleaned, the stroke kept as its original, or as drawn.</summary>
    public static Gesture FromStroke(Gesture gesture, IReadOnlyList<GesturePoint> stroke, bool cleanUp)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(stroke);
        var drawn = new GestureSample(stroke);
        return cleanUp ? gesture with { Samples = [Clean(drawn)], OriginalSamples = [drawn] } : gesture with { Samples = [drawn], OriginalSamples = null };
    }

    private static GestureSample Clean(GestureSample sample) => new(ShapeCleanup.Clean(sample).Points);
}
