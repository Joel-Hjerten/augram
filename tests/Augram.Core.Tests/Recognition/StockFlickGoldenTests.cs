using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;
using Xunit;

namespace Augram.Core.Tests.Recognition;

/// <summary>
/// Golden test: the eight stock flick templates (2-point samples) against noisy 300 px
/// straight strokes in each direction, under the StrokesPlus defaults (Legacy, Average,
/// P = 100, threshold 75). The correct flick must win with a score above 85.
/// </summary>
public sealed class StockFlickGoldenTests
{
    private const double StrokeLength = 300;
    private const int RawPoints = 30;
    private const double JitterPx = 2;

    private readonly GestureMatcher _matcher = new();
    private readonly IReadOnlyList<Core.Gestures.Gesture> _library = StockFlicks.All();

    public static TheoryData<string, int> Directions
    {
        get
        {
            var data = new TheoryData<string, int>();
            foreach (var name in StockFlicks.Directions.Keys)
            {
                foreach (var seed in new[] { 1, 2, 3 })
                {
                    data.Add(name, seed);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public void NoisyFlickMatchesItsStockGestureAbove85(string direction, int seed)
    {
        var stroke = StrokeBuilder.Jitter(StockFlicks.Stroke(direction, StrokeLength, RawPoints), JitterPx, seed);

        var match = _matcher.Match(stroke, _library, RecognitionOptions.Default);

        Assert.NotNull(match);
        Assert.Equal(direction, match.Name);
        Assert.True(match.Score > 85, $"{direction} seed {seed}: {match.Score}");
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public void RunnerUpIsANeighbouringDirectionWellBelowTheWinner(string direction, int seed)
    {
        var stroke = StrokeBuilder.Jitter(StockFlicks.Stroke(direction, StrokeLength, RawPoints), JitterPx, seed);

        var ranked = _matcher.Rank(stroke, _library, RecognitionOptions.Default);

        // A 45° neighbour scores about 100 − 50·0.99/2 ≈ 75 at best; the winner must be clearly ahead.
        Assert.True(ranked[0].Score - ranked[1].Score > 15, $"{direction}: {ranked[0].Name} {ranked[0].Score} vs {ranked[1].Name} {ranked[1].Score}");
    }
}
