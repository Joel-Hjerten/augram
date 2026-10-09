using Augram.Platform.MacOS.WindowSystem;

namespace Augram.Platform.MacOS.Overlay;

/// <summary>
/// The pure rules behind <see cref="MacTrailPanel"/>, tested on every OS: which display a stroke's panel covers, and where a
/// global top-left point lands in the panel's layer, whose origin is its bottom-left corner with y growing upwards.
/// </summary>
internal static class MacTrailGeometry
{
    /// <summary>What the panel covers when AppKit lists no screen at all.</summary>
    public static readonly MacRect Fallback = new(0, 0, 1920, 1080);

    /// <summary>The display containing the point; else the nearest one (a point on a display's far edge belongs to none); else <see cref="Fallback"/>.</summary>
    public static MacRect ScreenFor(IReadOnlyList<MacScreen> screens, double x, double y)
    {
        ArgumentNullException.ThrowIfNull(screens);
        MacRect? nearest = null;
        var best = double.MaxValue;
        foreach (var screen in screens)
        {
            var frame = screen.Frame;
            if (frame.Contains(x, y))
            {
                return frame;
            }

            var dx = Math.Max(Math.Max(frame.X - x, 0), x - frame.Right);
            var dy = Math.Max(Math.Max(frame.Y - y, 0), y - frame.Bottom);
            var distance = (dx * dx) + (dy * dy);
            if (distance < best)
            {
                best = distance;
                nearest = frame;
            }
        }

        return nearest ?? Fallback;
    }

    /// <summary>A global top-left point in the layer of a panel covering <paramref name="area"/>.</summary>
    public static (double X, double Y) ToLayer(double x, double y, MacRect area) => (x - area.X, area.Bottom - y);
}
