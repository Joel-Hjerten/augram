using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Core.Tests.Fixtures;
using Xunit;
using static Augram.Core.Tests.Fixtures.StrokeBuilder;

namespace Augram.Core.Tests.Recognition;

/// <summary>
/// Scale and position invariance (from arc-length resampling) and rotation SENSITIVITY
/// (from comparing raw headings): CLAUDE.md invariant 3.
/// </summary>
public sealed class InvarianceTests
{
    private readonly GestureMatcher _matcher = new();

    public static TheoryData<string, GesturePoint[]> Shapes => new()
    {
        { "L", Polyline(20, P(0, 0), P(0, 300), P(300, 300)) },
        { "Z", Polyline(20, P(0, 0), P(300, 0), P(0, 300), P(300, 300)) },
        { "arc", Arc(P(0, 0), 150, Math.PI, 2 * Math.PI, 40) },
    };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ScaledAndTranslatedCopyScoresAtLeast99(string name, GesturePoint[] stroke)
    {
        var copy = Translate(Scale(stroke, 3), 1234, -567);
        var gesture = TestGestures.Create(name, stroke);

        var score = Score(copy, gesture);

        Assert.True(score >= 99, $"{name}: {score}");
    }

    [Theory]
    [InlineData("Up", "Down")]
    [InlineData("Left", "Right")]
    [InlineData("UpLeft", "DownRight")]
    public void OppositeFlicksScoreBelow20(string template, string stroke)
    {
        var gesture = TestGestures.Create(template, StockFlicks.Template(template));

        var score = Score(StockFlicks.Stroke(stroke, 300, 30), gesture);

        Assert.True(score < 20, $"{template} vs {stroke}: {score}");
    }

    [Fact]
    public void MirroredLScoresWellBelowThreshold()
    {
        var l = Polyline(20, P(0, 0), P(0, 300), P(300, 300));
        var gesture = TestGestures.Create("L", l);

        var score = Score(Mirror(l), gesture);

        // The first leg matches, the second runs the other way: about half the headings differ by π.
        Assert.True(score < 60, $"L vs mirrored L: {score}");
    }

    private double Score(IReadOnlyList<GesturePoint> stroke, Gesture gesture)
        => _matcher.Rank(stroke, [gesture], RecognitionOptions.Default)[0].Score;
}
