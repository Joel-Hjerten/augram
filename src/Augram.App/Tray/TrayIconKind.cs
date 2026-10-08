namespace Augram.App.Tray;

/// <summary>Which tray image set a platform gets (see <see cref="TrayIconSet"/>).</summary>
internal enum TrayIconKind
{
    /// <summary>Windows: multi-size .ico files, falling back to the PNGs.</summary>
    WindowsIco,

    /// <summary>macOS: menu-bar template images.</summary>
    MacTemplate,

    /// <summary>Everything else: the 32 px PNGs.</summary>
    Png,
}
