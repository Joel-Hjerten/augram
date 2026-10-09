// WHY THIS IS ROTATION-SENSITIVE, AND WHY "LEGACY" DIVIDES BY THE PRECISION
//
// A stroke is described by the sequence of segment headings of its arc-length resample,
// and two strokes are compared heading by heading at the same index. Because the raw
// atan2 angles are compared directly, with no normalisation to a common heading, an
// up-flick (angle -pi/2) and a down-flick (angle +pi/2) differ by pi at every index and
// score near 0 against each other. That is the product: up, down, left and right are
// four different gestures (CLAUDE.md invariant 3). Never "fix" this by rotating strokes
// to a canonical heading the way $1-style recognizers do.
//
// Resampling by arc length is what makes the comparison scale- and position-invariant:
// a big "L" and a small "L" produce the same angle sequence.
//
// The original StrokesPlus code sizes its delta array at Precision (P) but a resample of
// P points yields only P-1 angles, so at least one zero delta is always averaged in and
// every score is diluted by the factor (P-1)/P. ScoringMode.Legacy reproduces that;
// ScoringMode.Corrected divides by the actual delta count and is the default, because
// StrokesPlus.net 0.5.8, which Joel's threshold of 75 and his templates were tuned with,
// scores that way (docs/learnings/0003-trigger-modifiers.md; Joel, 2026-10-09). See
// docs/reference/strokesplus-classic-source.md section 1 for the classic quirk.
using Augram.Core.Gestures;

namespace Augram.Core.Recognition;

/// <summary>
/// The angle math of the StrokesPlus classic / HighSign recognizer, ported verbatim
/// (StrokesPlusHook.cpp 5909–5929 and 5969–5980). See the comment at the top of this file.
/// </summary>
public static class AngleSequence
{
    /// <summary>100 / π: maps a mean angular delta in radians to a 0–100 score.</summary>
    public const double ProbabilityScale = 31.830988618379067;

    /// <summary>
    /// Heading of each segment between consecutive points (<c>GetPointArrayAngularMargins</c>).
    /// N points give N-1 angles; fewer than two points give none.
    /// </summary>
    public static double[] FromPoints(IReadOnlyList<GesturePoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
        {
            return [];
        }

        var angles = new double[points.Count - 1];
        for (int i = 1; i < points.Count; i++)
        {
            angles[i - 1] = Gradient(points[i - 1], points[i]);
        }

        return angles;
    }

    /// <summary>Heading of the segment from <paramref name="from"/> to <paramref name="to"/> (<c>GetAngularGradient</c>).</summary>
    public static double Gradient(GesturePoint from, GesturePoint to) => Math.Atan2(to.Y - from.Y, to.X - from.X);

    /// <summary>Unsigned difference between two headings, wrapped so it never exceeds π (<c>GetAngularDelta</c>).</summary>
    public static double AngularDelta(double angle1, double angle2)
    {
        double delta = Math.Abs(angle1 - angle2);

        if (delta > Math.PI)
        {
            delta = Math.PI - (delta - Math.PI);
        }

        return delta;
    }

    /// <summary>
    /// Mean angular delta to 0–100 score (<c>GetProbabilityFromAngularDelta</c>). A mean delta
    /// of 0 scores 100, of π scores 0; the <c>Abs</c> keeps the result non-negative.
    /// </summary>
    public static double Probability(double meanAngularDelta) => Math.Abs((meanAngularDelta * ProbabilityScale) - 100);
}
