using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Core.Training;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Hosting;

/// <summary>The engine's completed strokes reach an open training session first; wheel ticks never do.</summary>
public sealed class TrainingRoutingTests
{
    private static readonly GesturePoint[] UpStroke = [new(40, 300), new(40, 200), new(40, 100), new(40, 0)];

    [Fact]
    public void TryConsumeExtensionRoutesRecognizedAndNoMatchEventsButNotWheelTicks()
    {
        var session = new TrainingSession(new GestureLibrary(StarterGestures.All()), () => RecognitionOptions.Default);
        session.PublishCanvas(new ScreenArea(0, 0, 500, 500, Scale: 1));
        session.Begin(TrainingRequest.NewGesture);
        var points = UpStroke.Select(p => new CapturePoint((int)p.X, (int)p.Y, 0)).ToArray();
        var start = points[0];

        Assert.True(session.TryConsume(new EngineEvent.NoMatch("no", start, points, [])));
        Assert.True(session.TryConsume(new EngineEvent.GestureRecognized(StarterGestures.IdFor("Up"), "Up", 90, start, points, [])));
        Assert.False(session.TryConsume(new EngineEvent.WheelTriggered(WheelDirection.Up, start)));
        Assert.Equal(4, session.Stroke.Count);
    }
}
