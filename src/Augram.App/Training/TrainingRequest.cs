using Augram.Core.Gestures;

namespace Augram.App.Training;

/// <summary>
/// Why a training session opens: a new gesture (<see cref="GestureId"/> null) or one more sample for
/// an existing one ("Add sample" on a tile, F3). The resulting name is decided by the session.
/// </summary>
public sealed record TrainingRequest(GestureId? GestureId = null)
{
    public static TrainingRequest NewGesture { get; } = new();

    public static TrainingRequest AddSample(GestureId id) => new(id);

    public bool IsAddSample => GestureId is not null;
}
