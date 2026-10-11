using Augram.App.Components.GestureGlyph;
using Augram.App.Themes;
using Augram.Core.Config;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace Augram.App.Tests.Gestures;

/// <summary>The gradient's shaded start (Joel, 2026-10-08): darker, hue-shifted towards blue-violet the shorter way round.</summary>
public sealed class GlyphColoursTests
{
    [Theory]
    [InlineData(255, 255, 0, 48)] // yellow goes the warm way, through orange: amber
    [InlineData(0, 255, 0, 132)] // green: a darker, faintly teal green
    [InlineData(0, 255, 255, 192)] // cyan: a deeper blue
    [InlineData(255, 0, 0, 348)] // red: crimson
    public void ShadingRotatesTowardsBlueVioletTheShorterWay(byte r, byte g, byte b, double expectedHue)
    {
        var end = Color.FromRgb(r, g, b);
        var start = GlyphColours.StartFor(end);
        var (hue, _, value) = GlyphColours.ToHsv(start);

        Assert.Equal(expectedHue, hue, 0);
        Assert.Equal(GlyphColours.ToHsv(end).Value * GlyphColours.Darken, value, 2);
    }

    [Fact]
    public void JoelsYellowTrailStartsAmber()
    {
        var start = GlyphColours.StartFor(Color.Parse("#FFFF02"));

        Assert.True(start.R > start.G && start.G > start.B, $"amber has red over green over blue, got {start}");
        Assert.True(start.R < 255);
    }

    [Fact]
    public void AGreyOnlyDarkens()
    {
        var start = GlyphColours.StartFor(Color.FromRgb(200, 200, 200));

        Assert.Equal(start.R, start.G);
        Assert.Equal(start.G, start.B);
        Assert.Equal(160, start.R);
    }

    [Theory]
    [InlineData(255, 128, 0)]
    [InlineData(17, 99, 201)]
    [InlineData(250, 0, 250)]
    public void HsvRoundTrips(byte r, byte g, byte b)
    {
        var (hue, saturation, value) = GlyphColours.ToHsv(Color.FromRgb(r, g, b));

        Assert.Equal(Color.FromRgb(r, g, b), GlyphColours.FromHsv(hue, saturation, value));
    }

    [AvaloniaFact]
    public void TheLinkFollowsTheTrailColour()
    {
        var settings = new SettingsStore(Settings.Default with { Trail = Settings.Default.Trail with { Colour = new RgbColor(0, 255, 255) } });
        var resources = new ResourceDictionary();

        AppearanceLink.Follow(settings, resources, _ => { });
        Assert.Equal(Color.FromRgb(0, 255, 255), Brush(resources, AppearanceLink.GlyphEndKey));

        settings.SetTrail(settings.Current.Trail with { Colour = new RgbColor(255, 255, 0) });
        Dispatcher.UIThread.RunJobs();

        var end = Brush(resources, AppearanceLink.GlyphEndKey);
        var start = Brush(resources, AppearanceLink.GlyphStartKey);
        Assert.Equal(Color.FromRgb(255, 255, 0), end);
        Assert.Equal(GlyphColours.StartFor(end), start);
    }

    /// <summary>The dark theme's picture colours (the trail colour as it is, for a bright one).</summary>
    private static Color Brush(ResourceDictionary resources, string key)
    {
        Assert.True(resources.TryGetResource(key, Avalonia.Styling.ThemeVariant.Dark, out var value));
        return ((ISolidColorBrush)value!).Color;
    }
}
