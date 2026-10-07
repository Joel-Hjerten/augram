using Augram.Core.Gestures;

namespace Augram.Core.Recognition;

/// <summary>
/// The "likely to be confused" rule (checklist A7): each active gesture's first sample is scored as
/// if it were a stroke against every other active gesture; a pair scoring at or above the cut-off in
/// either direction is reported once, best score first. The cut-off is halfway between the match
/// threshold and a perfect score (87.5 at the default 75): under Legacy scoring two straight flicks
/// 45° apart already score 75 against each other, so the threshold itself would flag every
/// neighbouring flick in the starter set. A Core rule so the Gestures page only displays the
/// result (ADR-0002 §5a). Cost is one <see cref="GestureMatcher.Rank"/> per gesture.
/// </summary>
public static class ConfusionCheck
{
    /// <summary>At or above this score a pair is the same shape under two names (an "exact" duplicate, outlined strongly); between the cut-off and this it is merely close.</summary>
    public const double ExactCutOff = 95;

    public static double CutOff(RecognitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Threshold + ((100 - options.Threshold) / 2);
    }

    public static IReadOnlyList<ConfusionPair> Find(IReadOnlyList<Gesture> gestures, RecognitionOptions options, GestureMatcher? matcher = null)
        => Find(gestures, options, CutOff(options), matcher);

    public static IReadOnlyList<ConfusionPair> Find(IReadOnlyList<Gesture> gestures, RecognitionOptions options, double cutOff, GestureMatcher? matcher = null)
    {
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(options);
        matcher ??= new GestureMatcher();

        var active = gestures.Where(gesture => gesture.IsActive && gesture.Samples.Count > 0).ToList();
        var best = new Dictionary<(GestureId, GestureId), ConfusionPair>();
        foreach (var gesture in active)
        {
            foreach (var result in matcher.Rank(gesture.Samples[0], active, options))
            {
                if (result.GestureId == gesture.Id || result.Score < cutOff)
                {
                    continue;
                }

                var key = Ordered(gesture.Id, result.GestureId);
                if (!best.TryGetValue(key, out var existing) || existing.Score < result.Score)
                {
                    best[key] = new ConfusionPair(gesture.Id, gesture.Name, result.GestureId, result.Name, result.Score);
                }
            }
        }

        return best.Values
            .OrderByDescending(pair => pair.Score)
            .ThenBy(pair => pair.FirstName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (GestureId, GestureId) Ordered(GestureId a, GestureId b)
        => a.Value.CompareTo(b.Value) <= 0 ? (a, b) : (b, a);
}
