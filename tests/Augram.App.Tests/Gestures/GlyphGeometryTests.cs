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
}
