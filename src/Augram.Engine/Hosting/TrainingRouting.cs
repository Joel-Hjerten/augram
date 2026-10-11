using Augram.Core.Capture;
using Augram.Core.Training;

namespace Augram.Engine.Hosting;

/// <summary>
/// The one line the engine wiring needs: hand every completed stroke to the training session before
/// doing anything else with it (F3, A6). The App's composition root makes it the host's <see cref="EnginePorts.Intercept"/>.
/// Wheel ticks are never training input.
/// </summary>
public static class TrainingRouting
{
    /// <summary>True when the event is a completed stroke that the open training canvas consumed.</summary>
    public static bool TryConsume(this ITrainingSession session, EngineEvent engineEvent)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(engineEvent);
        return engineEvent switch
        {
            EngineEvent.GestureRecognized recognized => session.TryConsume(recognized.Points.ToGesturePoints(), recognized.Start.X, recognized.Start.Y),
            EngineEvent.NoMatch noMatch => session.TryConsume(noMatch.Points.ToGesturePoints(), noMatch.Start.X, noMatch.Start.Y),
            _ => false,
        };
    }
}
