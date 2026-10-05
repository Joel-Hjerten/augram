using Augram.Core.Gestures;

namespace Augram.App.Training;

/// <summary>
/// What the engine wiring calls after every completed stroke (F3, A6): a stroke that started over the
/// open training canvas belongs to the training session, not to recognition. Safe to call from the
/// engine worker thread; the session marshals to the UI itself.
/// </summary>
public interface ITrainingSession
{
    /// <summary>
    /// True when a training window is open and (<paramref name="startX"/>, <paramref name="startY"/>),
    /// in physical screen pixels, lies inside its draw canvas; the stroke then replaces the canvas's
    /// current stroke as if drawn with the left button. False means "not mine, carry on".
    /// </summary>
    bool TryConsume(IReadOnlyList<GesturePoint> points, int startX, int startY);
}
