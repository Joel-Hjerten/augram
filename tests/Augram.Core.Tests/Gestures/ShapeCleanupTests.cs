using Augram.Core.Gestures;
using Augram.Core.Gestures.Cleanup;
using Xunit;

namespace Augram.Core.Tests.Gestures;

/// <summary>
/// Shape cleanup (requirements F3, plan 0001 M2 step 10) over synthetic hand-drawn strokes: a seeded wobble on lines,
/// corners, arcs and circles. What must hold: lines come out straight and keep their angle (never snapped), corners stay
/// where they were drawn, circles and arcs keep the drawn direction, and the result is a plain point list.
/// </summary>
public sealed class ShapeCleanupTests
{
    [Fact]
    public void AWobblyTiltedLine_BecomesStraight_AndKeepsItsAngle()
    {
        var stroke = Wobble(Line(new(0, 0), new(173, 100), 60), 2.5, seed: 1);

        var cleaned = ShapeCleanup.Clean(stroke);

        Assert.Equal([PieceKind.Line], cleaned.Pieces);
        Assert.Equal(2, cleaned.Points.Count);
        Assert.Equal(30, AngleOf(cleaned.Points[0], cleaned.Points[^1]), 1);
        Assert.Equal(stroke[0], cleaned.Points[0]);
        Assert.Equal(stroke[^1], cleaned.Points[^1]);
    }

    [Fact]
    public void AnL_IsTwoLinesMeetingAtTheDrawnCorner()
    {
        var stroke = Wobble([.. Line(new(0, 0), new(0, 150), 50), .. Line(new(0, 150), new(120, 150), 40).Skip(1)], 2, seed: 2);

        var cleaned = ShapeCleanup.Clean(stroke);

        Assert.Equal([PieceKind.Line, PieceKind.Line], cleaned.Pieces);
        var corner = Assert.Single(cleaned.Corners);
        Assert.InRange(Distance(corner, new GesturePoint(0, 150)), 0, 12);
        Assert.Equal(3, cleaned.Points.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void AClosedWobblyLoop_IsACircleInTheDrawnDirection(int direction)
    {
        var stroke = Wobble(Arc(new(200, 200), 80, -Math.PI / 2, direction * 2 * Math.PI, 90), 3, seed: 3);

        var cleaned = ShapeCleanup.Clean(stroke);

        Assert.Equal([PieceKind.Circle], cleaned.Pieces);
        Assert.All(cleaned.Points, point => Assert.InRange(Distance(point, new GesturePoint(200, 200)), 74, 86));
        Assert.Equal(Math.Sign(direction), Math.Sign(Turn(cleaned.Points)));
        Assert.InRange(Distance(cleaned.Points[0], stroke[0]), 0, 10);
    }

    [Fact]
    public void AnUnevenHalfCircle_IsOneSmoothArc()
    {
        var stroke = Wobble(Arc(new(100, 100), 60, Math.PI, Math.PI, 50), 2, seed: 4);

        var cleaned = ShapeCleanup.Clean(stroke);

        Assert.Equal([PieceKind.Arc], cleaned.Pieces);
        Assert.Equal(stroke[0], cleaned.Points[0]);
        Assert.Equal(stroke[^1], cleaned.Points[^1]);
        Assert.All(cleaned.Points.Skip(1).SkipLast(1), point => Assert.InRange(Distance(point, new GesturePoint(100, 100)), 54, 66));
    }

    [Fact]
    public void AnUpFlickAndADownFlick_StayDifferent()
    {
        var up = ShapeCleanup.Clean(Wobble(Line(new(0, 200), new(0, 0), 40), 2, seed: 5));
        var down = ShapeCleanup.Clean(Wobble(Line(new(0, 0), new(0, 200), 40), 2, seed: 6));

        Assert.True(up.Points[0].Y > up.Points[^1].Y);
        Assert.True(down.Points[0].Y < down.Points[^1].Y);
    }

    [Fact]
    public void TooShortOrTinyStrokes_AreLeftAsDrawn()
    {
        GesturePoint[] two = [new(0, 0), new(10, 0)];
        GesturePoint[] dot = [new(5, 5), new(5.2, 5.1), new(5.3, 5.3)];

        Assert.Equal(two, ShapeCleanup.Clean(two).Points);
        Assert.Equal(dot, ShapeCleanup.Clean(dot).Points);
    }

    [Fact]
    public void TheResultIsDeterministic()
    {
        var stroke = Wobble([.. Line(new(0, 0), new(100, 0), 30), .. Arc(new(100, 50), 50, -Math.PI / 2, Math.PI, 30).Skip(1)], 2, seed: 7);

        Assert.Equal(ShapeCleanup.Clean(stroke).Points, ShapeCleanup.Clean(stroke).Points);
    }

    private static List<GesturePoint> Line(GesturePoint from, GesturePoint to, int count)
        => [.. Enumerable.Range(0, count + 1).Select(i => new GesturePoint(from.X + ((to.X - from.X) * i / count), from.Y + ((to.Y - from.Y) * i / count)))];

    private static List<GesturePoint> Arc(GesturePoint center, double radius, double start, double sweep, int count)
        => [.. Enumerable.Range(0, count + 1).Select(i => new GesturePoint(center.X + (radius * Math.Cos(start + (sweep * i / count))), center.Y + (radius * Math.Sin(start + (sweep * i / count)))))];

    /// <summary>A hand's wobble: each inner point moved up to <paramref name="amount"/> either way, the ends kept.</summary>
    private static List<GesturePoint> Wobble(List<GesturePoint> points, double amount, int seed)
    {
        var random = new Random(seed);
        return [.. points.Select((point, i) => i == 0 || i == points.Count - 1 ? point : new GesturePoint(point.X + ((random.NextDouble() * 2) - 1) * amount, point.Y + ((random.NextDouble() * 2) - 1) * amount))];
    }

    private static double AngleOf(GesturePoint from, GesturePoint to) => Math.Atan2(to.Y - from.Y, to.X - from.X) * 180 / Math.PI;

    private static double Distance(GesturePoint a, GesturePoint b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    /// <summary>The signed area (shoelace): positive turns one way, negative the other.</summary>
    private static double Turn(IReadOnlyList<GesturePoint> points)
    {
        var sum = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            sum += (points[i - 1].X * points[i].Y) - (points[i].X * points[i - 1].Y);
        }

        return sum;
    }
}
