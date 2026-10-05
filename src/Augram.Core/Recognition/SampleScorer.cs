namespace Augram.Core.Recognition;

/// <summary>
/// Scores one template angle sequence against one stroke angle sequence: the inner loop
/// of StrokesPlus classic <c>GetGestureName</c> (StrokesPlusHook.cpp 6636–6645).
/// </summary>
internal static class SampleScorer
{
    public static double Score(double[] templateAngles, double[] strokeAngles, int precision, ScoringMode mode)
    {
        if (templateAngles.Length == 0 || strokeAngles.Length == 0)
        {
            // Fewer than two distinct points on either side: nothing to compare.
            return 0;
        }

        // The original indexes the stroke's angles by the template's count; a shorter
        // stroke array would read out of bounds there. Compare what both sides have.
        int count = Math.Min(templateAngles.Length, strokeAngles.Length);

        double deltaSum = 0;
        for (int k = 0; k < count; k++)
        {
            deltaSum += AngleSequence.AngularDelta(templateAngles[k], strokeAngles[k]);
        }

        double divisor = mode == ScoringMode.Legacy ? precision : count;
        return AngleSequence.Probability(deltaSum / divisor);
    }
}
