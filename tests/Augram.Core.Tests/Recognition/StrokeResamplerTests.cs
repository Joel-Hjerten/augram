using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Xunit;
using static Augram.Core.Tests.Fixtures.StrokeBuilder;

namespace Augram.Core.Tests.Recognition;

public sealed class StrokeResamplerTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(250)]
    public void LongStrokeResamplesToExactlyNPoints(int segments)
    {
        var stroke = Polyline(50, P(0, 0), P(300, 0), P(300, 300), P(0, 300));

        var resampled = StrokeResampler.Resample(stroke, segments);

        Assert.Equal(segments, resampled.Length);
        Assert.Equal(stroke[0], resampled[0]);
    }

    [Theory]
    [InlineData(2, 100)]
    [InlineData(3, 100)]
    [InlineData(2, 10)]
    public void ShortStrokeResamplesToAtMostNPoints(int rawPoints, int segments)
    {
        var stroke = Line(P(0, 0), P(0, 50), rawPoints);

        var resampled = StrokeResampler.Resample(stroke, segments);

        Assert.InRange(resampled.Length, 2, segments);
        Assert.Equal(stroke[0], resampled[0]);
    }

    /// <summary>
    /// The original's early break guards against rounding emitting one point too many; the
    /// opposite (one too few) never happens in double arithmetic. Random polylines of 2 to
    /// 39 integer points at three precisions always give exactly N points. This is why the
    /// Legacy dilution is always exactly (N-1)/N (see <c>ScoringModeTests</c>).
    /// </summary>
    [Fact]
    public void DistinctStrokesAlwaysResampleToExactlyNPoints()
    {
        var random = new Random(7);

        for (int trial = 0; trial < 300; trial++)
        {
            var stroke = Enumerable.Range(0, random.Next(2, 40))
                .Select(_ => P(random.Next(0, 800), random.Next(0, 600)))
                .ToArray();
            if (StrokeResampler.PathLength(stroke) == 0)
            {
                continue;
            }

            foreach (var segments in new[] { 10, 50, 100 })
            {
                Assert.Equal(segments, StrokeResampler.Resample(stroke, segments).Length);
            }
        }
    }

    [Fact]
    public void ResampledPointsAreEvenlySpacedAlongTheStroke()
    {
        var stroke = Line(P(0, 0), P(0, 1000), 7);

        var resampled = StrokeResampler.Resample(stroke, 100);

        for (int i = 1; i < resampled.Length; i++)
        {
            Assert.Equal(10, resampled[i].Y - resampled[i - 1].Y, precision: 9);
        }
    }

    [Fact]
    public void EmptyInputGivesEmptyOutput()
    {
        Assert.Empty(StrokeResampler.Resample([], 100));
    }

    [Fact]
    public void CoincidentPointsGiveOnlyTheFirstPoint()
    {
        GesturePoint[] stroke = [P(5, 5), P(5, 5), P(5, 5)];

        var resampled = StrokeResampler.Resample(stroke, 100);

        Assert.Equal([P(5, 5)], resampled);
    }

    [Fact]
    public void PathLengthSumsSegments()
    {
        Assert.Equal(600, StrokeResampler.PathLength(Polyline(2, P(0, 0), P(300, 0), P(300, 300))));
    }
}
