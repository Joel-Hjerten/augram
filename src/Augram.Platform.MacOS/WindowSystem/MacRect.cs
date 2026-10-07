namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// A rectangle in global top-left points: origin at the top-left corner of the main display (the one with the menu
/// bar), y growing downwards. CoreGraphics window bounds, the Accessibility API and the hook's pointer positions all
/// use it; AppKit's bottom-left <c>NSScreen</c> frames are flipped into it with <see cref="FromCocoa"/>.
/// </summary>
internal readonly record struct MacRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>A Cocoa rectangle (origin bottom-left, y up, the main display's bottom edge at 0) in top-left coordinates.</summary>
    public static MacRect FromCocoa(double x, double y, double width, double height, double mainDisplayHeight)
        => new(x, mainDisplayHeight - (y + height), width, height);

    /// <summary>The inverse of <see cref="FromCocoa"/>: the Cocoa y of this rectangle's bottom-left origin.</summary>
    public double CocoaY(double mainDisplayHeight) => mainDisplayHeight - Bottom;

    public bool Contains(double x, double y) => x >= X && x < Right && y >= Y && y < Bottom;

    public double IntersectionArea(MacRect other)
    {
        var width = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        var height = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        return width > 0 && height > 0 ? width * height : 0;
    }

    /// <summary>Every edge within <paramref name="tolerance"/> points of the same edge of <paramref name="other"/>.</summary>
    public bool IsNear(MacRect other, double tolerance)
        => Math.Abs(X - other.X) <= tolerance
        && Math.Abs(Y - other.Y) <= tolerance
        && Math.Abs(Right - other.Right) <= tolerance
        && Math.Abs(Bottom - other.Bottom) <= tolerance;
}
