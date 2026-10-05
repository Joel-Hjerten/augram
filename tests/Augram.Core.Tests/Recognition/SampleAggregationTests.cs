using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;
using Xunit;

namespace Augram.Core.Tests.Recognition;

public sealed class SampleAggregationTests
{
    private readonly GestureMatcher _matcher = new();

    // One sample that matches an Up stroke perfectly (100) and one that is its opposite (1 under Legacy).
    private readonly Core.Gestures.Gesture _gesture = TestGestures.Create("Up", StockFlicks.Template("Up"), StockFlicks.Template("Down"));

    [Fact]
    public void AverageIsTheMeanOfThePerSampleScores()
    {
        var options = new RecognitionOptions(SampleAggregation: SampleAggregation.Average);

        var score = _matcher.Rank(StockFlicks.Template("Up"), [_gesture], options)[0].Score;

        Assert.Equal(50.5, score, precision: 9);
    }

    [Fact]
    public void BestIsTheHighestPerSampleScore()
    {
        var options = new RecognitionOptions(SampleAggregation: SampleAggregation.Best);

        var score = _matcher.Rank(StockFlicks.Template("Up"), [_gesture], options)[0].Score;

        Assert.Equal(100, score);
    }

    [Fact]
    public void AverageDoesNotMatchButBestDoes()
    {
        Assert.Null(_matcher.Match(StockFlicks.Template("Up"), [_gesture], new RecognitionOptions(SampleAggregation: SampleAggregation.Average)));
        Assert.NotNull(_matcher.Match(StockFlicks.Template("Up"), [_gesture], new RecognitionOptions(SampleAggregation: SampleAggregation.Best)));
    }
}
