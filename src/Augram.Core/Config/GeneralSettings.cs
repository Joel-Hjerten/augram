using Augram.Core.Capture;

namespace Augram.Core.Config;

/// <summary>
/// Settings that are not thresholds of one subsystem. The default stroke button is Right
/// while StrokesPlus.net still owns Middle on the development machine (session handoff;
/// revisit at M3).
/// </summary>
/// <param name="StrokeButton">The button that, held, starts a gesture.</param>
/// <param name="IgnoreKey">Modifiers that make the stroke button pass through.</param>
/// <param name="StartAtLogin">Register Augram with the OS login items.</param>
/// <param name="Enabled">The tray toggle: when false the hook passes everything through.</param>
/// <param name="ColourMenuBarIcon">macOS: the colour app icon in the menu bar instead of the single-colour shape the system tints (Joel, 2026-10-08, as in Eyeris).</param>
public sealed record GeneralSettings(
    MouseButton StrokeButton = MouseButton.Right,
    IgnoreKeys IgnoreKey = IgnoreKeys.None,
    bool StartAtLogin = false,
    bool Enabled = true,
    bool ColourMenuBarIcon = false)
{
    public static GeneralSettings Default { get; } = new();
}
