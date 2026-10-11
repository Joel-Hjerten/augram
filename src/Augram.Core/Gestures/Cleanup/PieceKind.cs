namespace Augram.Core.Gestures.Cleanup;

/// <summary>How one piece of a cleaned stroke was fitted.</summary>
public enum PieceKind
{
    /// <summary>A near-straight run, replaced by the straight line between its ends (its angle kept, never snapped).</summary>
    Line,

    /// <summary>A curve close to a circular arc, replaced by that arc, swept in the drawn direction.</summary>
    Arc,

    /// <summary>The whole stroke closes on itself like a circle: a full circle from where it started, in the drawn direction.</summary>
    Circle,

    /// <summary>Neither: the drawn points, gently smoothed, ends kept.</summary>
    Smooth,
}
