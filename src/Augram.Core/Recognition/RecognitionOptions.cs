namespace Augram.Core.Recognition;

/// <summary>
/// Tunables of the recognizer. Defaults are the StrokesPlus classic defaults
/// (<c>iPrecision = 100</c>, <c>MatchProbabilityThreshold = 75</c>).
/// </summary>
/// <param name="Precision">Number of points a stroke is resampled to; at least 2.</param>
/// <param name="Threshold">A gesture matches only if its score is strictly greater than this.</param>
/// <param name="ScoringMode">Legacy (divide by precision) or Corrected (divide by delta count).</param>
/// <param name="SampleAggregation">Average or Best across a gesture's training samples.</param>
public sealed record RecognitionOptions(
    int Precision = 100,
    double Threshold = 75,
    ScoringMode ScoringMode = ScoringMode.Legacy,
    SampleAggregation SampleAggregation = SampleAggregation.Average)
{
    public static RecognitionOptions Default { get; } = new();
}
