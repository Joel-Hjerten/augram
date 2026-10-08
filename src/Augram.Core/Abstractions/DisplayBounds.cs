namespace Augram.Core.Abstractions;

/// <summary>
/// Where a display sits on the desktop, in the coordinates the hook reports (physical pixels on Windows, points with
/// the origin at the main display's top-left on macOS), so a gesture's start point can be tested against it.
/// </summary>
public readonly record struct DisplayBounds(int X, int Y, int Width, int Height)
{
    /// <summary>True when the point lies inside; the right and bottom edges belong to the next display.</summary>
    public bool Contains(int x, int y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}
