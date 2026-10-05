namespace Augram.Core.Recognition;

/// <summary>How the summed angular deltas of a sample comparison are averaged.</summary>
public enum ScoringMode
{
    /// <summary>
    /// Divide by the precision P, exactly like StrokesPlus classic, even though at most
    /// P-1 deltas exist. Scores are diluted by (P-1)/P. Default: the threshold of 75 and
    /// existing templates were tuned against this. See the comment at the top of <c>AngleSequence.cs</c>.
    /// </summary>
    Legacy,

    /// <summary>Divide by the number of deltas actually compared.</summary>
    Corrected,
}
