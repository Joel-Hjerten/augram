using Augram.Core.Gestures;

namespace Augram.Core.Recognition;

/// <summary>
/// Scores a finished stroke against a gesture library: the outer loops of StrokesPlus
/// classic <c>GetGestureName</c> (StrokesPlusHook.cpp 6603–6668). Runs on button-up
/// only, never during capture (CLAUDE.md invariant 4). The stroke's angle sequence is
/// computed once per call (the original recomputed it per sample); template angle
/// sequences come from the <see cref="TemplateCache"/>.
/// </summary>
public sealed class GestureMatcher
{
    private readonly TemplateCache _templates;

    public GestureMatcher()
        : this(new TemplateCache())
    {
    }

    public GestureMatcher(TemplateCache templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        _templates = templates;
    }

    /// <summary>Every active gesture with its score, best first. Feeds the recognition log.</summary>
    public IReadOnlyList<MatchResult> Rank(IReadOnlyList<GesturePoint> stroke, IEnumerable<Gesture> gestures, RecognitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Precision, 2);

        var strokeAngles = AngleSequence.FromPoints(StrokeResampler.Resample(stroke, options.Precision));

        var results = new List<MatchResult>();
        foreach (var gesture in gestures)
        {
            if (!gesture.IsActive)
            {
                continue;
            }

            results.Add(new MatchResult(gesture.Id, gesture.Name, ScoreGesture(gesture, strokeAngles, options)));
        }

        // OrderByDescending is stable: equal scores keep library order.
        return results.OrderByDescending(result => result.Score).ToList();
    }

    /// <summary>The best-scoring active gesture if its score is strictly above the threshold, else null.</summary>
    public MatchResult? Match(IReadOnlyList<GesturePoint> stroke, IEnumerable<Gesture> gestures, RecognitionOptions options)
    {
        var ranked = Rank(stroke, gestures, options);
        if (ranked.Count == 0)
        {
            return null;
        }

        var best = ranked[0];
        return best.Score > options.Threshold && best.Score > 0 ? best : null;
    }

    private double ScoreGesture(Gesture gesture, double[] strokeAngles, RecognitionOptions options)
    {
        var samples = gesture.Samples;
        if (samples.Count == 0)
        {
            return 0;
        }

        double sum = 0;
        double best = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            var templateAngles = _templates.GetAngles(gesture.Id, i, samples[i], options.Precision);
            double score = SampleScorer.Score(templateAngles, strokeAngles, options.Precision, options.ScoringMode);
            sum += score;
            best = Math.Max(best, score);
        }

        return options.SampleAggregation == SampleAggregation.Best ? best : sum / samples.Count;
    }
}
