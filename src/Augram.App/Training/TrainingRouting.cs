using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Engine.Hosting;

namespace Augram.App.Training;

/// <summary>
/// The one line the engine wiring needs: hand every completed stroke to the training session before
/// doing anything else with it (F3, A6). Wheel ticks are never training input.
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
            EngineEvent.GestureRecognized recognized => session.TryConsume(ToGesturePoints(recognized.Points), recognized.Start.X, recognized.Start.Y),
            EngineEvent.NoMatch noMatch => session.TryConsume(ToGesturePoints(noMatch.Points), noMatch.Start.X, noMatch.Start.Y),
            _ => false,
        };
    }

    private static GesturePoint[] ToGesturePoints(IReadOnlyList<CapturePoint> points)
    {
        var result = new GesturePoint[points.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new GesturePoint(points[i].X, points[i].Y);
        }

        return result;
    }
}
