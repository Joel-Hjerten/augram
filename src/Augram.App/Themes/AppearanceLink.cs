using Augram.Core.Config;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Augram.App.Themes;

/// <summary>
/// Keeps the app looking the way Options › Appearance and the trail colour say (plan 0006), and is the one place that
/// reads them for the UI:
/// <list type="bullet">
/// <item>the theme: Dark, Light, or the system's (<see cref="ThemeVariant.Default"/>);</item>
/// <item>per theme variant, the accent's shades (<see cref="AccentPalette"/>; the trail colour while the accent follows
/// it) and the gesture pictures' <c>Brush.Glyph</c> / <c>Brush.GlyphStart</c> (Joel, 2026-10-08: pictures in the trail
/// colour, shaded from <c>GlyphColours.StartFor</c>);</item>
/// <item>the corner rounding tokens (<c>Radius.*</c>);</item>
/// <item>each attached window's backdrop (<see cref="WindowBackdrop"/>).</item>
/// </list>
/// Everything is written to the application's resources, which win over the theme's tokens; the tokens are what a root
/// without this link shows (tests, the previewer). Templates read these keys as dynamic resources, so a change applies at
/// once.
/// </summary>
public sealed class AppearanceLink
{
    public const string GlyphEndKey = "Brush.Glyph";
    public const string GlyphStartKey = "Brush.GlyphStart";

    private static readonly string[] AccentKeys = ["Accent.Fill", "Accent.OnFill", "Accent.Soft", "Accent.Line", "Accent.Edge", "Accent.Text"];

    private readonly IResourceDictionary _resources;
    private readonly Action<ThemeVariant> _setTheme;
    private readonly List<Window> _windows = [];
    private Settings _applied;

    private AppearanceLink(IResourceDictionary resources, Action<ThemeVariant> setTheme, Settings current)
    {
        _resources = resources;
        _setTheme = setTheme;
        _applied = current;
    }

    public AppearanceSettings Current => _applied.Appearance;

    public static AppearanceLink Follow(SettingsStore settings, Application app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return Follow(settings, app.Resources, variant => app.RequestedThemeVariant = variant);
    }

    /// <summary>The link over any resources and theme setter (tests use their own).</summary>
    internal static AppearanceLink Follow(SettingsStore settings, IResourceDictionary resources, Action<ThemeVariant> setTheme)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(setTheme);
        var link = new AppearanceLink(resources, setTheme, settings.Current);
        link.ApplyAll();
        settings.Changed += (_, _) =>
        {
            var current = settings.Current;
            Dispatcher.UIThread.Post(() => link.Update(current));
        };
        return link;
    }

    /// <summary>Gives <paramref name="window"/> the backdrop, and repaints its tint when its theme or the glass it got changes.</summary>
    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_windows.Contains(window))
        {
            return;
        }

        _windows.Add(window);
        WindowBackdrop.Apply(window, Current);
        window.ActualThemeVariantChanged += (_, _) => WindowBackdrop.Paint(window, Current);
        window.PropertyChanged += (_, change) =>
        {
            if (change.Property == TopLevel.ActualTransparencyLevelProperty)
            {
                WindowBackdrop.Paint(window, Current);
            }
        };
        window.Closed += (_, _) => _windows.Remove(window);
    }

    public static ThemeVariant VariantFor(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.System => ThemeVariant.Default,
        _ => ThemeVariant.Dark,
    };

    /// <summary>Writes the accent's and the pictures' brushes for Dark and Light into <paramref name="resources"/>' theme dictionaries.</summary>
    public static void ApplyColours(IResourceDictionary resources, RgbColor trail, RgbColor accent)
    {
        ArgumentNullException.ThrowIfNull(resources);
        foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            var palette = AccentPalette.For(variant == ThemeVariant.Dark, ToColor(accent), ToColor(trail));
            var dictionary = ThemeDictionary(resources, variant);
            dictionary["Accent.Fill"] = Brush(palette.Fill);
            dictionary["Accent.OnFill"] = Brush(palette.OnFill);
            dictionary["Accent.Soft"] = Brush(palette.Soft);
            dictionary["Accent.Line"] = Brush(palette.Line);
            dictionary["Accent.Edge"] = Brush(palette.Edge);
            dictionary["Accent.Text"] = Brush(palette.Text);
            dictionary[GlyphEndKey] = Brush(palette.GlyphEnd);
            dictionary[GlyphStartKey] = Brush(palette.GlyphStart);
        }
    }

    /// <summary>Takes back what <see cref="ApplyColours"/> wrote (tests that borrow the application's resources).</summary>
    internal static void RemoveColours(IResourceDictionary resources)
    {
        foreach (var provider in resources.ThemeDictionaries.Values)
        {
            if (provider is IResourceDictionary dictionary)
            {
                foreach (var key in AccentKeys.Append(GlyphEndKey).Append(GlyphStartKey))
                {
                    dictionary.Remove(key);
                }
            }
        }
    }

    /// <summary>The corner rounding (plan 0006 decision 4): panels at the setting, the page a little rounder, controls at half.</summary>
    public static void ApplyCorners(IResourceDictionary resources, int radius)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var tab = radius * 0.85;
        resources["Radius.Panel"] = new CornerRadius(radius);
        resources["Radius.Page"] = new CornerRadius(radius + 2);
        resources["Radius.Tab"] = new CornerRadius(tab, tab, 0, 0);
        resources["Radius.Control"] = new CornerRadius(radius / 2.0);
    }

    private void ApplyAll()
    {
        var appearance = _applied.Appearance;
        _setTheme(VariantFor(appearance.Theme));
        ApplyColours(_resources, _applied.Trail.Colour, AccentOf(_applied));
        ApplyCorners(_resources, appearance.CornerRadiusPx);
    }

    private void Update(Settings current)
    {
        var (was, now) = (_applied.Appearance, current.Appearance);
        var coloursChanged = current.Trail.Colour != _applied.Trail.Colour || AccentOf(current) != AccentOf(_applied);
        if (now == was && !coloursChanged)
        {
            return;
        }

        _applied = current;
        if (now.Theme != was.Theme)
        {
            _setTheme(VariantFor(now.Theme));
        }

        if (coloursChanged)
        {
            ApplyColours(_resources, current.Trail.Colour, AccentOf(current));
        }

        if (now.CornerRadiusPx != was.CornerRadiusPx)
        {
            ApplyCorners(_resources, now.CornerRadiusPx);
        }

        if (now.WindowBackground != was.WindowBackground || now.TintPercent != was.TintPercent)
        {
            foreach (var window in _windows)
            {
                WindowBackdrop.Apply(window, now);
            }
        }
    }

    private static RgbColor AccentOf(Settings settings) =>
        settings.Appearance.AccentFollowsTrail ? settings.Trail.Colour : settings.Appearance.Accent;

    private static IResourceDictionary ThemeDictionary(IResourceDictionary resources, ThemeVariant variant)
    {
        if (resources.ThemeDictionaries.TryGetValue(variant, out var provider) && provider is IResourceDictionary existing)
        {
            return existing;
        }

        var created = new ResourceDictionary();
        resources.ThemeDictionaries[variant] = created;
        return created;
    }

    private static Color ToColor(RgbColor colour) => Color.FromRgb(colour.R, colour.G, colour.B);

    private static ImmutableSolidColorBrush Brush(Color colour) => new(colour);
}
