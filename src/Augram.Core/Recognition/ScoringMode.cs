namespace Augram.Core.Recognition;

/// <summary>How the summed angular deltas of a sample comparison are averaged.</summary>
public enum ScoringMode
{
    /// <summary>
    /// Divide by the precision P, exactly like StrokesPlus classic (the C++ original), even though at most P-1 deltas
    /// exist, so scores are diluted by (P-1)/P and come out slightly higher. Kept for comparison for a while (Joel,
    /// 2026-10-09). See the comment at the top of <c>AngleSequence.cs</c>.
    /// </summary>
    Legacy,

    /// <summary>
    /// Divide by the number of deltas actually compared. The default (Joel, 2026-10-09): StrokesPlus.net 0.5.8, the
    /// version Joel's threshold of 75 and his gestures were tuned with, scores this way (learnings 0003).
    /// </summary>
    Corrected,
}
