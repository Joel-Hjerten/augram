namespace Augram.Core.Abstractions;

/// <summary>
/// Native fix-up for the trail overlay window (F6, learnings 0001 B2): the UI toolkit creates the
/// window, this port makes it click-through, non-activating and absent from the taskbar in whatever
/// way the OS needs (<c>WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW</c> on
/// Windows) and reads the result back, because the toolkit rewrites the styles on <c>Show()</c> and a
/// full-screen topmost window that is not click-through swallows every click on the machine. The App
/// decides from the report whether the window may be shown (<c>Overlay/OverlayStylePolicy</c>).
/// <see cref="Place"/> sizes the window in physical pixels, which the toolkit's DIP sizing gets wrong
/// across monitors of different DPI. The handle is opaque to Core. Platform implements it;
/// <see cref="NullOverlayWindowStyle"/> is the default.
/// </summary>
public interface IOverlayWindowStyle
{
    /// <summary>Applies the styles to the window behind <paramref name="handle"/> and returns what the OS reports afterwards. Idempotent.</summary>
    OverlayStyleReport Apply(nint handle);

    /// <summary>Moves and sizes the window to a rectangle in physical screen pixels without activating it.</summary>
    void Place(nint handle, int x, int y, int width, int height);
}
