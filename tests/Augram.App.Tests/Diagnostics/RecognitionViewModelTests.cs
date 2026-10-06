using Augram.App.ViewModels;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Xunit;

namespace Augram.App.Tests.Diagnostics;

/// <summary>The Recognition panel's glyph cells: the drawn stroke comes from the entry, the matched gesture from the library by id.</summary>
public sealed class RecognitionViewModelTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 1, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void MatchedPoints_ResolvesTheGestureFromTheLibrary()
    {
        var gesture = new Gesture(GestureId.New(), "Right", true, [new GestureSample([new GesturePoint(0, 0), new GesturePoint(100, 0)])]);
        var library = new GestureLibrary([gesture]);
        var vm = new RecognitionViewModel(new RecognitionLog(), library);
        GesturePoint[] stroke = [new(5, 5), new(90, 7)];
        var entry = new RecognitionLogEntry(T0, 2, 100, [new("Right", 91, gesture.Id)], MatchedGesture: gesture.Id, Stroke: stroke);

        Assert.Same(gesture.Samples[0], vm.MatchedPoints(entry));
        Assert.Same(stroke, RecognitionViewModel.DrawnPoints(entry));
        Assert.Equal("Right 91", RecognitionViewModel.Matched(entry));
    }

    [Fact]
    public void MatchedPoints_IsNullWhenNothingMatchedOrTheGestureIsGone()
    {
        var vm = new RecognitionViewModel(new RecognitionLog(), new GestureLibrary());
        var none = new RecognitionLogEntry(T0, 2, 100, [new("Right", 60)], NothingFiredReason: "below threshold");
        var gone = new RecognitionLogEntry(T0, 2, 100, [new("Right", 91)], MatchedGesture: GestureId.New());

        Assert.Null(vm.MatchedPoints(none));
        Assert.Null(vm.MatchedPoints(gone));
        Assert.Equal("–", RecognitionViewModel.Matched(none));
    }

    [Fact]
    public void Source_FollowsTheLog()
    {
        var log = new RecognitionLog();
        var vm = new RecognitionViewModel(log, new GestureLibrary());
        var changes = 0;
        vm.Source.Changed += (_, _) => changes++;

        log.Add(new RecognitionLogEntry(T0, 2, 100, [new("Right", 60)]));

        Assert.Equal(1, changes);
        Assert.Single(vm.Source.Items);
    }
}
