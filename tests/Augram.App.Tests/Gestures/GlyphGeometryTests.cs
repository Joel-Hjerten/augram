using Augram.App.Components.GestureGlyph;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Gestures;

public sealed class GlyphGeometryTests
{
    private static readonly Size Tile = new(64, 64);
    private static readonly Thickness Padding = new(6);

    [Fact]
    public void WideShapeFillsTheWidthAndKeepsItsAspectRatio()
    {
        GesturePoint[] points = [new(100, 200), new(300, 250)];

        var normalised = GlyphGeometry.Normalise(points, Tile, Padding);

        var width = normalised.Max(p => p.X) - normalised.Min(p => p.X);
        var height = normalised.Max(p => p.Y) - normalised.Min(p => p.Y);
        Assert.Equal(52, width, 6);
        Assert.Equal(13, height, 6);
        Assert.Equal(4.0, width / height, 6);
        Assert.All(normalised, p => Assert.InRange(p.X, 6, 58));
        Assert.All(normalised, p => Assert.InRange(p.Y, 6, 58));
        Assert.Equal(32, (normalised.Max(p => p.Y) + normalised.Min(p => p.Y)) / 2, 6);
    }

    [Fact]
    public void VerticalLineIsCentredHorizontallyAndFillsTheHeight()
    {
        GesturePoint[] points = [new(50, 500), new(50, 100)];

        var normalised = GlyphGeometry.Normalise(points, Tile, Padding);

        Assert.All(normalised, p => Assert.Equal(32, p.X, 6));
        Assert.Equal(58, normalised[0].Y, 6);
        Assert.Equal(6, normalised[1].Y, 6);
    }

    [Fact]
    public void ScaleIsInvariant()
    {
        GesturePoint[] small = [new(0, 0), new(10, 0), new(10, 5)];
        var large = small.Select(p => new GesturePoint(p.X * 37, p.Y * 37)).ToArray();

        var a = GlyphGeometry.Normalise(small, Tile, Padding);
        var b = GlyphGeometry.Normalise(large, Tile, Padding);

        Assert.Equal(a.Length, b.Length);
        for (var i = 0; i < a.Length; i++)
        {
            Assert.Equal(a[i].X, b[i].X, 6);
            Assert.Equal(a[i].Y, b[i].Y, 6);
        }
    }

    [Fact]
    public void ArrowWingsSweepBackFromTheTip()
    {
        var (left, right) = GlyphGeometry.ArrowWings(new Point(0, 0), new Point(10, 0), 4);

        Assert.True(left.X < 10 && right.X < 10, "wings lie behind the tip");
        Assert.Equal(left.X, right.X, 6);
        Assert.Equal(-left.Y, right.Y, 6);
        Assert.Equal(4, Math.Sqrt(Math.Pow(left.X - 10, 2) + Math.Pow(left.Y, 2)), 6);
    }

    [AvaloniaFact]
    public void BuildProducesAGeometryInsideTheTile()
    {
        var geometry = GlyphGeometry.Build(StarterGestures.All().Single(g => g.Name == "Circle").Samples[0], Tile, Padding, 8);

        var bounds = geometry.Bounds;
        Assert.True(bounds.Width > 40 && bounds.Height > 40, bounds.ToString());
        Assert.True(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= 64 && bounds.Bottom <= 64, bounds.ToString());
    }

    [AvaloniaFact]
    public void EmptyAndSinglePointInputsDoNotThrow()
    {
        Assert.Empty(GlyphGeometry.Normalise([], Tile, Padding));
        Assert.NotNull(GlyphGeometry.Build([new GesturePoint(3, 3)], Tile, Padding, 8));
        Assert.NotNull(GlyphGeometry.BuildRaw([], 8));
    }

    [Fact]
    public void ProgressRunsByArcLengthFromZeroToOne()
    {
        // The second point is a third of the way along by length, not halfway by index.
        Assert.Equal([0, 1.0 / 3, 1], GlyphGeometry.Progress([new Point(0, 0), new Point(1, 0), new Point(3, 0)]));
        Assert.Equal([0.0], GlyphGeometry.Progress([new Point(5, 5)]));
        Assert.Empty(GlyphGeometry.Progress([]));
    }

    [Fact]
    public void StrokePointsAreNormalisedWithRepeatsDropped()
    {
        var points = GlyphGeometry.StrokePoints([new GesturePoint(0, 0), new GesturePoint(0, 0), new GesturePoint(10, 0)], Tile, Padding);

        Assert.Equal(2, points.Length);
        Assert.Equal(Padding.Left, points[0].X, 6);
        Assert.Equal(Tile.Width - Padding.Right, points[1].X, 6);
    }

    [Fact]
    public void TheGradientRunsFromTheStartColourToTheEndColourInSteps()
    {
        var start = Avalonia.Media.Color.FromRgb(0, 0, 0);
        var end = Avalonia.Media.Color.FromRgb(150, 150, 150);

        Assert.Equal(start, GlyphStroke.ShadeAt(start, end, 0));
        Assert.Equal(end, GlyphStroke.ShadeAt(start, end, 1));
        Assert.Equal(GlyphStroke.ShadeAt(start, end, 0.5), GlyphStroke.ShadeAt(start, end, 0.51));
        Assert.Equal(GlyphStroke.Steps, Enumerable.Range(0, 1001).Select(i => GlyphStroke.ShadeAt(start, end, i / 1000.0)).Distinct().Count());
    }
}
