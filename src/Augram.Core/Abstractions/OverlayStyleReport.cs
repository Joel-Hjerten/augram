namespace Augram.Core.Abstractions;

/// <summary>
/// What the OS reports about the overlay window after <see cref="IOverlayWindowStyle.Apply"/>.
/// <see cref="ClickThrough"/> is the one that must hold before the window may cover the screen;
/// the others are logged. <see cref="Raw"/> is the native style for the log line.
/// </summary>
/// <param name="ClickThrough">Mouse input passes to the windows underneath.</param>
/// <param name="NoActivate">The window never takes the foreground or keyboard focus.</param>
/// <param name="ToolWindow">No taskbar button, not in Alt-Tab.</param>
/// <param name="Raw">The native style as text, e.g. <c>0x82000A8</c>; <c>n/a</c> when the platform has none.</param>
public sealed record OverlayStyleReport(bool ClickThrough, bool NoActivate, bool ToolWindow, string Raw)
{
    /// <summary>A platform with nothing to apply reports every property as satisfied.</summary>
    public static OverlayStyleReport NotApplicable { get; } = new(true, true, true, "n/a");
}
