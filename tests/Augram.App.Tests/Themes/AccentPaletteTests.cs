using Augram.App.Components.GestureGlyph;
using Augram.App.Themes;
using Avalonia.Media;
using Xunit;

namespace Augram.App.Tests.Themes;

/// <summary>Plan 0006 decision 6: one colour in, each theme's accent shades out; light keeps fills bright.</summary>
public sealed class AccentPaletteTests
{
    private static readonly Color Yellow = Color.Parse("#F5C542");
    private static readonly Color Green = Color.Parse("#00FF40");

    [Fact]
    public void YellowStaysTheSameYellowAsAFillInBothThemes()
    {
        Assert.Equal(Yellow, AccentPalette.For(dark: true, Yellow, Yellow).Fill);
        Assert.Equal(Yellow, AccentPalette.For(dark: false, Yellow, Yellow).Fill);
    }

    [Fact]
    public void LightThemeTextInTheAccentIsDarkEnoughToRead()
    {
        var light = AccentPalette.For(dark: false, Yellow, Yellow);

        Assert.True(AccentPalette.Contrast(light.Text, Colors.White) >= 4.5);
        Assert.True(AccentPalette.Contrast(light.Fill, Colors.White) < 3, "the fill must not be darkened to the text's shade");
    }

    [Fact]
    public void ABrightFillGetsDarkTextAndADeeperEdgeInTheLightTheme()
    {
        var light = AccentPalette.For(dark: false, Yellow, Yellow);

        Assert.Equal(Color.FromRgb(23, 20, 10), light.OnFill);
        Assert.NotEqual(0, light.Edge.A);
        Assert.Equal(0, AccentPalette.For(dark: true, Yellow, Yellow).Edge.A);
    }

    [Fact]
    public void NeonGreenIsDarkenedOnlyAsFarAsWhiteNeeds()
    {
        var fill = AccentPalette.For(dark: false, Green, Green).Fill;

        Assert.NotEqual(Green, fill);
        Assert.InRange(AccentPalette.Contrast(fill, Colors.White), 1.6, 1.8);
    }

    [Fact]
    public void ADarkAccentIsLightenedUntilItShowsOnTheDarkTheme()
    {
        var navy = Color.Parse("#1E2A6E");
        var dark = AccentPalette.For(dark: true, navy, navy);

        Assert.True(AccentPalette.Contrast(dark.Fill, Color.FromRgb(42, 41, 47)) >= 3);
        Assert.True(AccentPalette.Contrast(dark.Text, Color.FromRgb(42, 41, 47)) >= 5);
    }

    [Fact]
    public void PicturesFollowTheTrailAndStartWhereGlyphColoursSays()
    {
        var palette = AccentPalette.For(dark: false, Color.Parse("#5B9BFF"), Yellow);

        Assert.NotEqual(Yellow, palette.GlyphEnd);
        Assert.True(AccentPalette.Contrast(palette.GlyphEnd, Color.FromRgb(244, 244, 247)) >= 2);
        Assert.Equal(GlyphColours.StartFor(palette.GlyphEnd), palette.GlyphStart);
        Assert.Equal(Yellow, AccentPalette.For(dark: true, Yellow, Yellow).GlyphEnd);
    }
}
