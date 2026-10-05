using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;
using Xunit;

namespace Augram.Core.Tests.Recognition;

public sealed class GestureMatcherTests
{
    private readonly GestureMatcher _matcher = new();

    [Fact]
    public void ScoreEqualToThresholdDoesNotMatch()
    {
        // A template against itself scores exactly 100 (every delta is 0).
        var up = TestGestures.Create("Up", StockFlicks.Template("Up"));

        Assert.Null(_matcher.Match(StockFlicks.Template("Up"), [up], new RecognitionOptions(Threshold: 100)));
        Assert.NotNull(_matcher.Match(StockFlicks.Template("Up"), [up], new RecognitionOptions(Threshold: 99.999)));
    }

    [Fact]
    public void InactiveGesturesAreNeverMatchedNorRanked()
    {
        var inactive = TestGestures.Create("Up (off)", isActive: false, StockFlicks.Template("Up"));
        var active = TestGestures.Create("Down", StockFlicks.Template("Down"));

        var ranked = _matcher.Rank(StockFlicks.Template("Up"), [inactive, active], RecognitionOptions.Default);
        var match = _matcher.Match(StockFlicks.Template("Up"), [inactive, active], RecognitionOptions.Default);

        Assert.Equal(["Down"], ranked.Select(r => r.Name));
        Assert.Null(match);
    }

    [Fact]
    public void RankSortsByScoreDescendingAndCarriesIds()
    {
        var gestures = StockFlicks.All();

        var ranked = _matcher.Rank(StockFlicks.Stroke("Right", 300, 2), gestures, RecognitionOptions.Default);

        Assert.Equal(gestures.Count, ranked.Count);
        Assert.Equal("Right", ranked[0].Name);
        Assert.Equal(gestures.Single(g => g.Name == "Right").Id, ranked[0].GestureId);
        Assert.Equal(ranked.OrderByDescending(r => r.Score), ranked);
    }

    [Fact]
    public void StrokeWithFewerThanTwoDistinctPointsScoresZero()
    {
        var up = TestGestures.Create("Up", StockFlicks.Template("Up"));
        GesturePoint[] dot = [new(10, 10), new(10, 10)];

        Assert.Equal(0, _matcher.Rank(dot, [up], RecognitionOptions.Default)[0].Score);
        Assert.Equal(0, _matcher.Rank([new(10, 10)], [up], RecognitionOptions.Default)[0].Score);
        Assert.Null(_matcher.Match(dot, [up], new RecognitionOptions(Threshold: -1)));
    }

    [Fact]
    public void SampleWithFewerThanTwoDistinctPointsScoresZero()
    {
        var dot = TestGestures.Create("Dot", [new GesturePoint(3, 3), new GesturePoint(3, 3)]);

        Assert.Equal(0, _matcher.Rank(StockFlicks.Template("Up"), [dot], RecognitionOptions.Default)[0].Score);
    }

    [Fact]
    public void GestureWithoutSamplesScoresZero()
    {
        var empty = TestGestures.Create("Empty");

        Assert.Equal(0, _matcher.Rank(StockFlicks.Template("Up"), [empty], RecognitionOptions.Default)[0].Score);
    }

    [Fact]
    public void NoGesturesGivesNoMatch()
    {
        Assert.Empty(_matcher.Rank(StockFlicks.Template("Up"), [], RecognitionOptions.Default));
        Assert.Null(_matcher.Match(StockFlicks.Template("Up"), [], RecognitionOptions.Default));
    }

    [Fact]
    public void PrecisionBelowTwoIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _matcher.Rank(StockFlicks.Template("Up"), [], new RecognitionOptions(Precision: 1)));
    }
}
