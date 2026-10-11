using Augram.Core.Config;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Augram.App.Themes;

/// <summary>
/// A window's background (plan 0006 decisions 2 and 3): the system's glass behind it, and the theme's <c>Layer.Window</c>
/// colour painted over it at the user's tint. The operating system does the blur, so Augram never captures the screen,
/// and the strength of the blur is the system's. When the system gives no glass (Solid, transparency effects off,
/// battery saver, an unfocused Mica window, a platform without it), the tint is painted opaque, so the window is
/// never see-through without a backdrop behind it.
/// </summary>
public static class WindowBackdrop
{
    /// <summary>What to ask the system for, best first; Avalonia takes the first the platform supports.</summary>
    public static IReadOnlyList<WindowTransparencyLevel> HintsFor(WindowBackground background, bool windows) => background switch
    {
        WindowBackground.FrostedGlass => [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None],
        // Mica is Windows 11's; elsewhere the system blur, with a stronger tint (TintOpacity).
        WindowBackground.WallpaperTint when windows => [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None],
        WindowBackground.WallpaperTint => [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None],
        _ => [WindowTransparencyLevel.None],
    };

    /// <summary>
    /// The opacity of the tint over what the system actually gives (<paramref name="actual"/>). The system's glass already
    /// carries its own tint, so 0 % shows it as it is and 100 % covers it. Wallpaper tint off Windows stands in for Mica
    /// with a blur under at least half the tint (lead's call; plan 0006 step 1 tunes it).
    /// </summary>
    public static double TintOpacity(WindowBackground background, int tintPercent, WindowTransparencyLevel actual, bool windows)
    {
        if (actual == WindowTransparencyLevel.None || background == WindowBackground.Solid)
        {
            return 1;
        }

        var tint = Math.Clamp(tintPercent, 0, 100) / 100.0;
        return background == WindowBackground.WallpaperTint && !windows ? 0.55 + (0.45 * tint) : tint;
    }

    public static void Apply(Window window, AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(appearance);
        window.TransparencyLevelHint = HintsFor(appearance.WindowBackground, OperatingSystem.IsWindows());
        Paint(window, appearance);
    }

    /// <summary>Paints the tint for the window's current theme and the glass the system actually gave it.</summary>
    public static void Paint(Window window, AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(appearance);
        var colour = window.TryFindResource("Layer.Window", window.ActualThemeVariant, out var value) && value is Color c ? c : Colors.Black;
        var opacity = TintOpacity(appearance.WindowBackground, appearance.TintPercent, window.ActualTransparencyLevel, OperatingSystem.IsWindows());
        window.Background = new ImmutableSolidColorBrush(colour, opacity);
    }
}
