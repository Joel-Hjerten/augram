using Augram.Core.Gestures;

namespace Augram.Core.Tests.Fixtures;

/// <summary>
/// The eight stock flick templates as 2-point samples. "Up" is StrokesPlus.net's stock
/// template (0,500) to (0,100); the others are the same 400 px flick about the same
/// centre (0,300) in the other seven compass directions. Screen coordinates: Y grows downwards.
/// </summary>
internal static class StockFlicks
{
    private const double HalfLength = 200;
    private static readonly GesturePoint Centre = new(0, 300);
    private static readonly double Diagonal = Math.Sqrt(0.5);

    /// <summary>Unit direction vectors by stock name.</summary>
    public static IReadOnlyDictionary<string, (double Dx, double Dy)> Directions { get; } = new Dictionary<string, (double, double)>
    {
        ["Up"] = (0, -1),
        ["Down"] = (0, 1),
        ["Left"] = (-1, 0),
        ["Right"] = (1, 0),
        ["UpLeft"] = (-Diagonal, -Diagonal),
        ["UpRight"] = (Diagonal, -Diagonal),
        ["DownLeft"] = (-Diagonal, Diagonal),
        ["DownRight"] = (Diagonal, Diagonal),
    };

    public static GesturePoint[] Template(string name)
    {
        var (dx, dy) = Directions[name];
        return
        [
            new GesturePoint(Centre.X - (HalfLength * dx), Centre.Y - (HalfLength * dy)),
            new GesturePoint(Centre.X + (HalfLength * dx), Centre.Y + (HalfLength * dy)),
        ];
    }

    /// <summary>All eight gestures, each with its single 2-point template.</summary>
    public static IReadOnlyList<Gesture> All()
        => Directions.Keys.Select(name => TestGestures.Create(name, Template(name))).ToArray();

    /// <summary>A straight flick of <paramref name="length"/> px in the named direction, starting at the origin.</summary>
    public static GesturePoint[] Stroke(string name, double length, int pointCount)
    {
        var (dx, dy) = Directions[name];
        return StrokeBuilder.Line(new GesturePoint(0, 0), new GesturePoint(dx * length, dy * length), pointCount);
    }
}
