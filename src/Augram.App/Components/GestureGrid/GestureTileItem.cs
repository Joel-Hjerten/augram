using Augram.Core.Gestures;

namespace Augram.App.Components.GestureGrid;

/// <summary>What one tile of the <see cref="GestureGrid"/> shows: a projection of a <see cref="Gesture"/> (its first sample is the glyph).</summary>
public sealed record GestureTileItem(GestureId Id, string Name, bool IsActive, IReadOnlyList<GesturePoint>? Points, int SampleCount)
{
    public static GestureTileItem From(Gesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        return new GestureTileItem(gesture.Id, gesture.Name, gesture.IsActive, gesture.Samples.Count > 0 ? gesture.Samples[0] : null, gesture.Samples.Count);
    }
}
