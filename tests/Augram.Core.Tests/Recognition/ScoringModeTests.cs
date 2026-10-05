using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;
using Xunit;

namespace Augram.Core.Tests.Recognition;

/// <summary>
/// Legacy divides the delta sum by the precision P, Corrected by the number of deltas
/// compared. A resample of P points carries P-1 headings, so the two modes differ for
/// every imperfect match; the gap widens as P shrinks.
/// </summary>
public sealed class ScoringModeTests
{
    private readonly GestureMatcher _matcher = new();

    /// <summary>
    /// A Right template against a Down stroke: every heading differs by exactly π/2.
    /// With d deltas compared, Legacy = |d·(π/2)/P · 100/π − 100| = 100 − 50·d/P and
    /// Corrected = |(π/2) · 100/π − 100| = 50, whatever d is.
    /// </summary>
    [Theory]
    [InlineData(100, 50.5)]
    [InlineData(10, 55.0)]
    [InlineData(4, 62.5)]
    public void PerpendicularLinesScoreFiftyCorrectedAndDilutedLegacy(int precision, double expectedLegacy)
    {
        var right = TestGestures.Create("Right", StockFlicks.Template("Right"));
        var down = StockFlicks.Stroke("Down", 300, 2);

        var legacy = Score(down, right, new RecognitionOptions(precision, ScoringMode: ScoringMode.Legacy));
        var corrected = Score(down, right, new RecognitionOptions(precision, ScoringMode: ScoringMode.Corrected));

        Assert.Equal(expectedLegacy, legacy, precision: 9);
        Assert.Equal(50, corrected, precision: 9);
    }

    /// <summary>
    /// An "L" against its mirror image. Both resample to 100 points, so 99 deltas exist:
    /// the corner lands exactly on resampled point 50, giving 50 first-leg deltas of 0 and
    /// 49 second-leg deltas of π. Legacy = |49π/100 · 100/π − 100| = 51.0;
    /// Corrected = |49π/99 · 100/π − 100| = 100 − 4900/99 = 50.505.
    /// Note: in double arithmetic a stroke never resamples to fewer than P points (see
    /// <see cref="StrokeResamplerTests.DistinctStrokesAlwaysResampleToExactlyNPoints"/>), so
    /// the P-1 headings in P slots is the whole of the Legacy dilution.
    /// </summary>
    [Fact]
    public void MirroredLScores51LegacyAnd50Point5Corrected()
    {
        var l = StrokeBuilder.Polyline(20, new GesturePoint(0, 0), new GesturePoint(0, 300), new GesturePoint(300, 300));
        var gesture = TestGestures.Create("L", l);
        var stroke = StrokeBuilder.Mirror(l);

        var resampledCount = StrokeResampler.Resample(stroke, 100).Length;
        var legacy = Score(stroke, gesture, new RecognitionOptions(ScoringMode: ScoringMode.Legacy));
        var corrected = Score(stroke, gesture, new RecognitionOptions(ScoringMode: ScoringMode.Corrected));

        Assert.Equal(100, resampledCount);
        Assert.Equal(51.0, legacy, precision: 9);
        Assert.Equal(100 - (4900.0 / 99), corrected, precision: 9);
    }

    [Fact]
    public void PerfectMatchScoresExactlyOneHundredInBothModes()
    {
        var up = StockFlicks.Template("Up");
        var gesture = TestGestures.Create("Up", up);

        Assert.Equal(100, Score(up, gesture, new RecognitionOptions(ScoringMode: ScoringMode.Legacy)));
        Assert.Equal(100, Score(up, gesture, new RecognitionOptions(ScoringMode: ScoringMode.Corrected)));
    }

    private double Score(IReadOnlyList<GesturePoint> stroke, Gesture gesture, RecognitionOptions options)
        => _matcher.Rank(stroke, [gesture], options)[0].Score;
}
