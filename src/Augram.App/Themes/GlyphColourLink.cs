using Augram.App.Components.GestureGlyph;
using Augram.Core.Config;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace Augram.App.Themes;

/// <summary>
/// Gesture pictures in the user's trail colour (Joel, 2026-10-08): keeps the <c>Brush.Glyph</c> and <c>Brush.GlyphStart</c>
/// resources, which the glyph template reads as dynamic resources, at Options › Appearance › Trail › Colour and its shaded start
/// (<see cref="GlyphColours.StartFor"/>). Set once at startup and again whenever the trail colour changes; the tokens in
/// <c>Tokens.axaml</c> are what a root without this link (tests, the previewer) shows. Inactive pictures keep the muted
/// greys.
/// </summary>
public static class GlyphColourLink
{
    public const string EndKey = "Brush.Glyph";
    public const string StartKey = "Brush.GlyphStart";

    public static void Follow(SettingsStore settings, IResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(resources);
        var applied = settings.Current.Trail.Colour;
        Apply(resources, applied);
        settings.Changed += (_, _) =>
        {
            var colour = settings.Current.Trail.Colour;
            if (colour == applied)
            {
                return;
            }

            applied = colour;
            Dispatcher.UIThread.Post(() => Apply(resources, colour));
        };
    }

    public static void Apply(IResourceDictionary resources, RgbColor trail)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var end = Color.FromRgb(trail.R, trail.G, trail.B);
        resources[EndKey] = new ImmutableSolidColorBrush(end);
        resources[StartKey] = new ImmutableSolidColorBrush(GlyphColours.StartFor(end));
    }
}
