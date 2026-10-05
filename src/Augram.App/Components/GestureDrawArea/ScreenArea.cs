using Augram.Core.Gestures;

namespace Augram.App.Components.GestureDrawArea;

/// <summary>
/// Where a control sits on screen, in physical screen pixels (the hook's coordinate space), plus the
/// render scaling that maps those pixels back to the control's logical coordinates. Immutable so the
/// engine worker can read the latest one without a lock.
/// </summary>
public sealed record ScreenArea(double Left, double Top, double Width, double Height, double Scale)
{
    public bool Contains(double x, double y)
        => x >= Left && y >= Top && x < Left + Width && y < Top + Height;

    /// <summary>A screen point as the control would see it (logical pixels, origin at the control's top-left).</summary>
    public GesturePoint ToLocal(GesturePoint screen)
        => new((screen.X - Left) / Scale, (screen.Y - Top) / Scale);
}
