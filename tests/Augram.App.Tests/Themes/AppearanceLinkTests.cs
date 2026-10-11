using Augram.App.Themes;
using Augram.Core.Config;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace Augram.App.Tests.Themes;

/// <summary>Plan 0006: Options › Appearance and the trail colour reach the UI through one link.</summary>
public sealed class AppearanceLinkTests
{
    private static readonly RgbColor Yellow = new(245, 197, 66);
    private static readonly RgbColor Blue = new(91, 155, 255);

    [AvaloniaFact]
    public void TheThemeFollowsTheSetting()
    {
        var settings = new SettingsStore(Settings.Default);
        var theme = ThemeVariant.Default;

        AppearanceLink.Follow(settings, new ResourceDictionary(), variant => theme = variant);
        Assert.Equal(ThemeVariant.Dark, theme);

        settings.SetAppearance(settings.Current.Appearance with { Theme = AppTheme.Light });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ThemeVariant.Light, theme);

        settings.SetAppearance(settings.Current.Appearance with { Theme = AppTheme.System });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ThemeVariant.Default, theme);
    }

    [AvaloniaFact]
    public void TheAccentFollowsTheTrailUntilTheSwitchIsOff()
    {
        var settings = new SettingsStore(Settings.Default with { Trail = Settings.Default.Trail with { Colour = Yellow } });
        var resources = new ResourceDictionary();
        AppearanceLink.Follow(settings, resources, _ => { });
        Assert.Equal(Color.FromRgb(245, 197, 66), Fill(resources, ThemeVariant.Dark));

        settings.SetAppearance(settings.Current.Appearance with { AccentFollowsTrail = false, Accent = Blue });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Color.FromRgb(91, 155, 255), Fill(resources, ThemeVariant.Dark));

        settings.SetTrail(settings.Current.Trail with { Colour = new RgbColor(0, 255, 64) });
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Color.FromRgb(91, 155, 255), Fill(resources, ThemeVariant.Dark));
    }

    [AvaloniaFact]
    public void EachThemeGetsItsOwnShades()
    {
        var resources = new ResourceDictionary();

        AppearanceLink.ApplyColours(resources, Yellow, Yellow);

        Assert.True(resources.TryGetResource("Accent.Text", ThemeVariant.Dark, out var dark));
        Assert.True(resources.TryGetResource("Accent.Text", ThemeVariant.Light, out var light));
        Assert.NotEqual(((ISolidColorBrush)dark!).Color, ((ISolidColorBrush)light!).Color);
    }

    [AvaloniaFact]
    public void CornerRoundingSetsEveryRadiusToken()
    {
        var settings = new SettingsStore(Settings.Default);
        var resources = new ResourceDictionary();
        AppearanceLink.Follow(settings, resources, _ => { });
        Assert.Equal(new CornerRadius(12), resources["Radius.Panel"]);

        settings.SetAppearance(settings.Current.Appearance with { CornerRadiusPx = 4 });
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new CornerRadius(4), resources["Radius.Panel"]);
        Assert.Equal(new CornerRadius(6), resources["Radius.Page"]);
        Assert.Equal(new CornerRadius(2), resources["Radius.Control"]);
    }

    [Fact]
    public void TheBackdropAsksForGlassAndFallsBackToNone()
    {
        Assert.Equal(WindowTransparencyLevel.AcrylicBlur, WindowBackdrop.HintsFor(WindowBackground.FrostedGlass, windows: true)[0]);
        Assert.Equal(WindowTransparencyLevel.Mica, WindowBackdrop.HintsFor(WindowBackground.WallpaperTint, windows: true)[0]);
        Assert.Equal(WindowTransparencyLevel.AcrylicBlur, WindowBackdrop.HintsFor(WindowBackground.WallpaperTint, windows: false)[0]);
        Assert.Equal([WindowTransparencyLevel.None], WindowBackdrop.HintsFor(WindowBackground.Solid, windows: true));
        Assert.All(
            [WindowBackground.FrostedGlass, WindowBackground.WallpaperTint],
            background => Assert.Equal(WindowTransparencyLevel.None, WindowBackdrop.HintsFor(background, windows: true)[^1]));
    }

    [Fact]
    public void TheTintIsOpaqueWheneverTheSystemGivesNoGlass()
    {
        Assert.Equal(1, WindowBackdrop.TintOpacity(WindowBackground.FrostedGlass, 62, WindowTransparencyLevel.None, windows: true));
        Assert.Equal(1, WindowBackdrop.TintOpacity(WindowBackground.Solid, 0, WindowTransparencyLevel.AcrylicBlur, windows: true));
        Assert.Equal(0.62, WindowBackdrop.TintOpacity(WindowBackground.FrostedGlass, 62, WindowTransparencyLevel.AcrylicBlur, windows: true), 3);
        Assert.Equal(0, WindowBackdrop.TintOpacity(WindowBackground.WallpaperTint, 0, WindowTransparencyLevel.Mica, windows: true));
        Assert.True(WindowBackdrop.TintOpacity(WindowBackground.WallpaperTint, 0, WindowTransparencyLevel.AcrylicBlur, windows: false) >= 0.5);
    }

    private static Color Fill(ResourceDictionary resources, ThemeVariant variant)
    {
        Assert.True(resources.TryGetResource("Accent.Fill", variant, out var value));
        return ((ISolidColorBrush)value!).Color;
    }
}
