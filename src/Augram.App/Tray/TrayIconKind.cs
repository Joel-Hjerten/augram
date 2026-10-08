namespace Augram.App.Tray;

/// <summary>Which tray image set a platform gets (see <see cref="TrayIconSet"/>).</summary>
internal enum TrayIconKind
{
    /// <summary>Windows: multi-size .ico files, falling back to the PNGs.</summary>
    WindowsIco,

    /// <summary>macOS: menu-bar template images.</summary>
    MacTemplate,

    /// <summary>macOS with Colour menu-bar icon on: the app icon in colour, 36 px.</summary>
    MacColour,

    /// <summary>Everything else: the 32 px PNGs.</summary>
    Png,
}
