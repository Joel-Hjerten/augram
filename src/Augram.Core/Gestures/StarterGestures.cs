using System.Security.Cryptography;
using System.Text;

namespace Augram.Core.Gestures;

/// <summary>
/// The fresh-install gesture set (plan 0001 C3): eight flicks, four out-and-backs, four
/// L-shapes, C, S, Z and a circle, as raw point lists drawn once at 100 px scale in screen
/// coordinates (Y grows downwards). Ids are derived from the names, so two installs agree
/// on them and an export from one merges cleanly into the other.
/// </summary>
public static class StarterGestures
{
    private const double StepPx = 10;

    public static IReadOnlyList<Gesture> All()
    {
        return
        [
            Make("Up", Segment(P(50, 100), P(50, 0))),
            Make("Down", Segment(P(50, 0), P(50, 100))),
            Make("Left", Segment(P(100, 50), P(0, 50))),
            Make("Right", Segment(P(0, 50), P(100, 50))),
            Make("Up Left", Segment(P(100, 100), P(0, 0))),
            Make("Up Right", Segment(P(0, 100), P(100, 0))),
            Make("Down Left", Segment(P(100, 0), P(0, 100))),
            Make("Down Right", Segment(P(0, 0), P(100, 100))),
            Make("Up and Back", Polyline(P(50, 100), P(50, 0), P(50, 100))),
            Make("Down and Back", Polyline(P(50, 0), P(50, 100), P(50, 0))),
            Make("Left and Back", Polyline(P(100, 50), P(0, 50), P(100, 50))),
            Make("Right and Back", Polyline(P(0, 50), P(100, 50), P(0, 50))),
            Make("L Down Right", Polyline(P(0, 0), P(0, 100), P(100, 100))),
            Make("L Down Left", Polyline(P(100, 0), P(100, 100), P(0, 100))),
            Make("L Up Right", Polyline(P(0, 100), P(0, 0), P(100, 0))),
            Make("L Up Left", Polyline(P(100, 100), P(100, 0), P(0, 0))),
            Make("C", Arc(P(50, 50), 50, -60, -300, 25)),
            Make("S", [.. Arc(P(50, 25), 25, -45, -270, 13), .. Arc(P(50, 75), 25, -90, 135, 13).Skip(1)]),
            Make("Z", Polyline(P(0, 0), P(100, 0), P(0, 100), P(100, 100))),
            Make("Circle", Arc(P(50, 50), 50, -90, 270, 37)),
        ];
    }

    /// <summary>A stable id for a starter name: the first 16 bytes of SHA-256("augram.starter:" + name).</summary>
    public static GestureId IdFor(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("augram.starter:" + name));
        return new GestureId(new Guid(hash.AsSpan(0, 16)));
    }

    private static Gesture Make(string name, IEnumerable<GesturePoint> points)
        => new(IdFor(name), name, IsActive: true, [new GestureSample(points)]);

    private static GesturePoint P(double x, double y) => new(x, y);

    /// <summary>A straight leg with a point every <see cref="StepPx"/> or so, both ends included.</summary>
    private static List<GesturePoint> Segment(GesturePoint from, GesturePoint to)
    {
        double length = Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
        int steps = Math.Max(1, (int)Math.Round(length / StepPx));
        var points = new List<GesturePoint>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            points.Add(P(from.X + (t * (to.X - from.X)), from.Y + (t * (to.Y - from.Y))));
        }

        return points;
    }

    private static List<GesturePoint> Polyline(params GesturePoint[] corners)
    {
        var points = new List<GesturePoint> { corners[0] };
        for (int i = 1; i < corners.Length; i++)
        {
            points.AddRange(Segment(corners[i - 1], corners[i]).Skip(1));
        }

        return points;
    }

    /// <summary>Arc in degrees, screen orientation: 0° is right, 90° is down, so increasing angles turn clockwise.</summary>
    private static List<GesturePoint> Arc(GesturePoint centre, double radius, double startDegrees, double endDegrees, int pointCount)
    {
        var points = new List<GesturePoint>(pointCount);
        for (int i = 0; i < pointCount; i++)
        {
            double degrees = startDegrees + ((endDegrees - startDegrees) * i / (pointCount - 1));
            double radians = degrees * Math.PI / 180;
            points.Add(P(
                Math.Round(centre.X + (radius * Math.Cos(radians)), 2),
                Math.Round(centre.Y + (radius * Math.Sin(radians)), 2)));
        }

        return points;
    }
}
