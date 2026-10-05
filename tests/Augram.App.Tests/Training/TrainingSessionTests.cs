using Augram.App.Components.GestureDrawArea;
using Augram.App.Training;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Engine.Hosting;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Training;

public sealed class TrainingSessionTests
{
    private static readonly GesturePoint[] UpStroke = [new(40, 300), new(40, 200), new(40, 100), new(40, 0)];

    [Fact]
    public void BeginPrefillsTheNextFreeNameAndAddSampleUsesTheTargetsName()
    {
        var library = new GestureLibrary(StarterGestures.All());
        library.Add(new Gesture(GestureId.New(), "New gesture 1", IsActive: true, [new GestureSample(UpStroke)]));
        var session = new TrainingSession(library, () => RecognitionOptions.Default);

        session.Begin(TrainingRequest.NewGesture);
        Assert.Equal("New gesture 2", session.Name);
        Assert.True(session.IsOpen);

        session.Begin(TrainingRequest.AddSample(StarterGestures.IdFor("Circle")));
        Assert.Equal("Circle", session.Name);
        Assert.Equal("Circle", session.Target!.Name);
    }

    [Fact]
    public void AcceptAddsANewGestureOrAppendsASampleAndRedrawReplaces()
    {
        var library = new GestureLibrary(StarterGestures.All());
        var session = new TrainingSession(library, () => RecognitionOptions.Default);
        var outcomes = new List<TrainingOutcome>();
        session.Ended += (_, outcome) => outcomes.Add(outcome);

        session.Begin(TrainingRequest.NewGesture);
        session.Name = "North";
        session.ReplaceStroke([new(0, 0), new(100, 100)]);
        session.ReplaceStroke(UpStroke);
        var added = session.Accept();

        Assert.Equal("North", added.Name);
        Assert.Equal(UpStroke, Assert.Single(added.Samples));
        Assert.False(session.IsOpen);

        session.Begin(TrainingRequest.AddSample(added.Id));
        session.ReplaceStroke(UpStroke);
        var updated = session.Accept();

        Assert.Equal(added.Id, updated.Id);
        Assert.Equal(2, updated.Samples.Count);
        Assert.Equal([TrainingOutcome.Accepted, TrainingOutcome.Accepted], outcomes);
    }

    [Fact]
    public void AcceptWithoutAStrokeOrWithATakenNameIsRejectedWithAMessage()
    {
        var session = new TrainingSession(new GestureLibrary(StarterGestures.All()), () => RecognitionOptions.Default);
        session.Begin(TrainingRequest.NewGesture);

        Assert.Equal("Draw the gesture first.", Assert.Throws<GestureValidationException>(session.Accept).Message);

        session.ReplaceStroke(UpStroke);
        session.Name = "up";
        Assert.Contains("'Up' already exists", Assert.Throws<GestureValidationException>(session.Accept).Message, StringComparison.Ordinal);
        Assert.True(session.IsOpen);
    }

    [AvaloniaFact]
    public void TryConsumeTakesOnlyStrokesStartingInsideTheCanvasWhileOpen()
    {
        var session = new TrainingSession(new GestureLibrary(StarterGestures.All()), () => RecognitionOptions.Default);
        session.PublishCanvas(new ScreenArea(1000, 500, 400, 500, Scale: 2));
        var screenStroke = UpStroke.Select(p => new GesturePoint(p.X + 1100, p.Y + 600)).ToArray();

        Assert.False(session.TryConsume(screenStroke, 1140, 900), "closed session");

        session.Begin(TrainingRequest.NewGesture);
        Assert.False(session.TryConsume(screenStroke, 999, 600), "outside");
        Assert.True(session.TryConsume(screenStroke, 1140, 900), "inside");

        Assert.Equal(4, session.Stroke.Count);
        Assert.Equal(new GesturePoint(70, 200), session.Stroke[0]);
        Assert.Equal("Up", session.BestMatch!.Name);

        session.PublishCanvas(null);
        Assert.False(session.TryConsume(screenStroke, 1140, 900), "no canvas");
    }

    [AvaloniaFact]
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
