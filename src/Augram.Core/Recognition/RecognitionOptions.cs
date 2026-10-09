namespace Augram.Core.Recognition;

/// <summary>
/// Tunables of the recognizer. Defaults are StrokesPlus.net's (<c>MatchPrecision = 100</c>,
/// <c>MatchProbabilityThreshold = 75</c>, Corrected scoring, samples averaged; learnings 0003).
/// </summary>
/// <param name="Precision">Number of points a stroke is resampled to; at least 2.</param>
/// <param name="Threshold">A gesture matches only if its score is strictly greater than this.</param>
/// <param name="ScoringMode">Corrected (divide by delta count, the default) or Legacy (divide by precision, the classic C++ quirk).</param>
/// <param name="SampleAggregation">Average or Best across a gesture's training samples.</param>
public sealed record RecognitionOptions(
    int Precision = 100,
    double Threshold = 75,
    ScoringMode ScoringMode = ScoringMode.Corrected,
    SampleAggregation SampleAggregation = SampleAggregation.Average)
{
    public static RecognitionOptions Default { get; } = new();
}
