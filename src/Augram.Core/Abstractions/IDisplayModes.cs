namespace Augram.Core.Abstractions;

/// <summary>
/// Reads and changes the displays' modes and HDR (the Display steps; learnings 0002). Implemented by Platform.Windows
/// (legacy display settings API, CCD for names and HDR) and Platform.MacOS (CoreGraphics; no HDR);
/// <see cref="NullDisplayModes"/> knows no displays. Every member may take a while (a mode change can take a second or
/// two while the display re-syncs) and is called from the command executor thread, or from the UI thread to fill a
/// step form; never from a hook handler. The adapter applies what Core resolved and never guesses: a mode handed to
/// <see cref="SetMode"/> is one of <see cref="DisplayInfo.Modes"/>.
/// </summary>
public interface IDisplayModes
{
    /// <summary>Which platform this adapter is, for the not-supported reasons.</summary>
    HostPlatform Platform { get; }

    /// <summary>Whether this platform lets an app switch HDR at all (false on macOS).</summary>
    bool CanSwitchHdr { get; }

    /// <summary>The active displays as they are now, the main one among them; empty when none can be read.</summary>
    IReadOnlyList<DisplayInfo> Displays();

    /// <summary>Applies <paramref name="mode"/> to <paramref name="display"/> in one change and stores it as the OS's settings page would.</summary>
    DisplayChangeResult SetMode(DisplayInfo display, VideoMode mode);

    /// <summary>Turns HDR on or off on <paramref name="display"/>, stored as the OS's settings page would.</summary>
    DisplayChangeResult SetHdr(DisplayInfo display, bool on);
}
