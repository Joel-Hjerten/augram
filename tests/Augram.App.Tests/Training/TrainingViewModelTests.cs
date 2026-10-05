using Augram.App.Training;
using Augram.App.ViewModels;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Xunit;

namespace Augram.App.Tests.Training;

public sealed class TrainingViewModelTests
{
    private static readonly GesturePoint[] RightStroke = [new(0, 50), new(50, 50), new(100, 50), new(150, 50)];

    [Fact]
    public void AStrokeSetsTheLiveBestMatchAndRedrawReplacesIt()
    {
        var (vm, _, _) = Create();
        Assert.Equal("Draw a stroke to see what it looks like", vm.BestMatchText);
        Assert.False(vm.CanAccept);

        vm.StrokeDrawn(RightStroke);

        Assert.StartsWith("Looks like Right, ", vm.BestMatchText, StringComparison.Ordinal);
        Assert.EndsWith("%", vm.BestMatchText, StringComparison.Ordinal);
        Assert.True(vm.CanAccept);

        vm.StrokeDrawn(RightStroke.Select(p => new GesturePoint(p.Y, p.X)).ToArray());

        Assert.StartsWith("Looks like Down, ", vm.BestMatchText, StringComparison.Ordinal);
        Assert.Equal(4, vm.Stroke.Count);
    }

    [Fact]
    public void AcceptAddsToTheLibraryAndEndsTheSession()
    {
        var (vm, session, library) = Create();
        var ended = new List<TrainingOutcome>();
        vm.Ended += (_, outcome) => ended.Add(outcome);
        vm.Name = "East";
        vm.StrokeDrawn(RightStroke);

        vm.AcceptCommand.Execute(null);

        Assert.Contains(library.All, g => g.Name == "East" && g.IsActive && g.Samples.Count == 1);
        Assert.Equal([TrainingOutcome.Accepted], ended);
        Assert.False(session.IsOpen);
    }

    [Fact]
    public void RejectedAcceptShowsTheRuleMessageInlineAndKeepsTheWindowOpen()
    {
        var (vm, session, library) = Create();
        vm.Name = "right";
        vm.StrokeDrawn(RightStroke);

        vm.Accept();

        Assert.Equal("A gesture named 'Right' already exists.", vm.Message);
        Assert.True(session.IsOpen);
        Assert.Equal(StarterGestures.All().Count, library.All.Count);

        vm.Name = "East";
        Assert.Null(vm.Message);
    }

    [Fact]
    public void CancelEndsWithoutStoringAnything()
    {
        var (vm, session, library) = Create();
        var ended = new List<TrainingOutcome>();
        vm.Ended += (_, outcome) => ended.Add(outcome);
        vm.StrokeDrawn(RightStroke);

        vm.CancelCommand.Execute(null);

        Assert.Equal([TrainingOutcome.Cancelled], ended);
        Assert.False(session.IsOpen);
        Assert.Equal(StarterGestures.All().Count, library.All.Count);
    }

    private static (TrainingViewModel Vm, TrainingSession Session, GestureLibrary Library) Create()
    {
        var library = new GestureLibrary(StarterGestures.All());
        var session = new TrainingSession(library, () => RecognitionOptions.Default);
        session.Begin(TrainingRequest.NewGesture);
        return (new TrainingViewModel(session), session, library);
    }
}
