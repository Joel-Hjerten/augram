using Augram.Core.Gestures;

namespace Augram.App.Training;

/// <summary>
/// Why a training session opens: a new gesture (<see cref="GestureId"/> null) or a redraw of an
/// existing one ("Redraw" on a tile, F3: Accept replaces its sample). The name is decided by the session.
/// </summary>
public sealed record TrainingRequest(GestureId? GestureId = null)
{
    public static TrainingRequest NewGesture { get; } = new();

    public static TrainingRequest Redraw(GestureId id) => new(id);

    public bool IsRedraw => GestureId is not null;
}
