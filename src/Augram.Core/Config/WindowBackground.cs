namespace Augram.Core.Config;

/// <summary>
/// Options › Appearance › Window background (plan 0006 decision 2): what shows behind Augram's windows. The operating system
/// draws the glass, so Augram never captures the screen; where the system shows it solid (transparency effects off, battery
/// saver), every choice looks like <see cref="Solid"/>.
/// </summary>
public enum WindowBackground
{
    /// <summary>The default: blurs whatever is behind the window (Windows 11's Acrylic, the macOS system blur).</summary>
    FrostedGlass,

    /// <summary>Picks up only the wallpaper's colour (Windows 11's Mica; on macOS the system blur with a stronger tint).</summary>
    WallpaperTint,

    /// <summary>No glass.</summary>
    Solid,
}
