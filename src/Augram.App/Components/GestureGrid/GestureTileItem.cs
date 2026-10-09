using Augram.Core.Gestures;
using Augram.Core.Recognition;

namespace Augram.App.Components.GestureGrid;

/// <summary>
/// What one tile of the <see cref="GestureGrid"/> shows: a projection of a <see cref="Gesture"/> (its
/// first sample is the glyph) plus its duplicates (A7: other gestures scoring at or above
/// <see cref="ConfusionCheck.DuplicateCutOff"/> against it), which outline the tile and light up when it is selected.
/// </summary>
public sealed record GestureTileItem(GestureId Id, string Name, bool IsActive, IReadOnlyList<GesturePoint>? Points, int SampleCount, IReadOnlyList<GesturePartner>? Partners = null)
{
    public IReadOnlyList<GesturePartner> Partners { get; init; } = Partners ?? [];

    public bool HasDuplicates => Partners.Count > 0;

    /// <summary>The samples are a cleaned shape, the drawn ones kept: the menu offers Restore original instead of Clean up shape.</summary>
    public bool IsCleanedUp { get; init; }

    public static GestureTileItem From(Gesture gesture) => From(gesture, []);

    /// <summary>Projects the gesture and picks its partners out of <paramref name="pairs"/> (found at the duplicate cut-off), best first.</summary>
    public static GestureTileItem From(Gesture gesture, IReadOnlyList<ConfusionPair> pairs)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(pairs);
        var partners = pairs
            .Where(pair => pair.FirstId == gesture.Id || pair.SecondId == gesture.Id)
            .Select(pair => pair.FirstId == gesture.Id
                ? new GesturePartner(pair.SecondId, pair.SecondName, pair.Score)
                : new GesturePartner(pair.FirstId, pair.FirstName, pair.Score))
            .OrderByDescending(partner => partner.Score)
            .ToList();
        return new GestureTileItem(gesture.Id, gesture.Name, gesture.IsActive, gesture.Samples.Count > 0 ? gesture.Samples[0] : null, gesture.Samples.Count, partners) { IsCleanedUp = gesture.IsCleanedUp };
    }
}
