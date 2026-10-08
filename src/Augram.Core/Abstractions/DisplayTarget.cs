namespace Augram.Core.Abstractions;

/// <summary>
/// Which display a Display step acts on (Joel, 2026-10-08): the one the gesture was drawn on by default, so with two
/// displays each changes its own; or the main display, which is what Display Changer always changed.
/// </summary>
public enum DisplayTarget
{
    /// <summary>The display containing the gesture start point.</summary>
    UnderGesture,

    /// <summary>The main (primary) display, wherever the gesture was drawn.</summary>
    Main,
}
