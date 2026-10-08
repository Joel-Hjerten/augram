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
    [InlineData(255, 255, 0, 40)] // yellow goes the warm way, through orange: amber
    [InlineData(0, 255, 0, 140)] // green: a darker, faintly teal green
    [InlineData(0, 255, 255, 200)] // cyan: a deeper blue
    [InlineData(255, 0, 0, 340)] // red: crimson
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
        Assert.Equal(140, start.R);
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

        GlyphColourLink.Follow(settings, resources);
        Assert.Equal(Color.FromRgb(0, 255, 255), ((ISolidColorBrush)resources[GlyphColourLink.EndKey]!).Color);

        settings.SetTrail(settings.Current.Trail with { Colour = new RgbColor(255, 255, 0) });
        Dispatcher.UIThread.RunJobs();

        var end = ((ISolidColorBrush)resources[GlyphColourLink.EndKey]!).Color;
        var start = ((ISolidColorBrush)resources[GlyphColourLink.StartKey]!).Color;
        Assert.Equal(Color.FromRgb(255, 255, 0), end);
        Assert.Equal(GlyphColours.StartFor(end), start);
    }
}
