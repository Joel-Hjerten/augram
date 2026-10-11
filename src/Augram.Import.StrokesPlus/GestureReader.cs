using System.Text.Json;
using Augram.Core.Gestures;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads <c>Gestures[]</c> into Core <see cref="Gesture"/> records. One <see cref="GestureSample"/>
/// per <c>PointPattern</c>, ordered by <c>Order</c>; samples with fewer than two distinct points are
/// dropped, gestures with no usable sample are skipped, and a repeated name gets a numbered suffix.
/// </summary>
internal sealed class GestureReader
{
    private const string FallbackName = "Unnamed gesture";
    private readonly List<ImportWarning> _warnings;
    private readonly ImportedNames _names;

    public GestureReader(List<ImportWarning> warnings)
    {
        _warnings = warnings;
        _names = new ImportedNames("gesture", warnings);
    }

    public IReadOnlyList<Gesture> Read(JsonElement root)
    {
        if (!JsonRead.TryArray(root, StrokesPlusJson.Gestures, out var gestures))
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, StrokesPlusJson.Gestures, "No Gestures array found; nothing to import."));
            return [];
        }

        var result = new List<Gesture>();
        var index = 0;
        foreach (var element in gestures.EnumerateArray())
        {
            index++;
            if (element.ValueKind != JsonValueKind.Object)
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, "#" + index, "Gesture entry is not an object; skipped."));
                continue;
            }

            var gesture = ReadGesture(element, index);
            if (gesture is not null)
            {
                result.Add(gesture);
            }
        }

        return result;
    }

    private Gesture? ReadGesture(JsonElement element, int index)
    {
        var sourceName = ReadName(element, index);
        var samples = ReadSamples(element, sourceName);
        if (samples.Count == 0)
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, sourceName, "No usable training sample; gesture skipped."));
            return null;
        }

        if (samples.All(sample => sample.Count == 2))
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Info, sourceName, "Looks like a stock StrokesPlus.net gesture (2-point template)."));
        }

        var isActive = JsonRead.Flag(element, StrokesPlusJson.Gesture.Active, whenAbsent: true);
        return new Gesture(GestureId.New(), _names.Claim(sourceName), isActive, samples);
    }

    private static string ReadName(JsonElement element, int index)
    {
        var name = JsonRead.Text(element, StrokesPlusJson.Gesture.Name);
        return name.Length == 0 ? FallbackName + " " + index : name;
    }

    private List<GestureSample> ReadSamples(JsonElement gesture, string sourceName)
    {
        var samples = new List<GestureSample>();
        if (!JsonRead.TryArray(gesture, StrokesPlusJson.Gesture.PointPatterns, out var patterns))
        {
            return samples;
        }

        var ordered = patterns.EnumerateArray()
            .Where(pattern => pattern.ValueKind == JsonValueKind.Object)
            .Select((pattern, position) => (Pattern: pattern, Order: ReadOrder(pattern, position), Position: position))
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Position);

        foreach (var (pattern, _, position) in ordered)
        {
            var points = ReadPoints(pattern);
            if (points.Distinct().Count() < 2)
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, sourceName, "Sample " + (position + 1) + " has fewer than 2 distinct points; dropped."));
                continue;
            }

            samples.Add(new GestureSample(points));
        }

        return samples;
    }

    private static int ReadOrder(JsonElement pattern, int position)
    {
        return pattern.TryGetProperty(StrokesPlusJson.PointPattern.Order, out var order) && order.TryGetInt32(out var value)
            ? value
            : position;
    }

    private static List<GesturePoint> ReadPoints(JsonElement pattern)
    {
        var points = new List<GesturePoint>();
        if (!JsonRead.TryArray(pattern, StrokesPlusJson.PointPattern.Points, out var array))
        {
            return points;
        }

        foreach (var point in array.EnumerateArray())
        {
            if (TryReadCoordinate(point, StrokesPlusJson.Point.X, out var x) && TryReadCoordinate(point, StrokesPlusJson.Point.Y, out var y))
            {
                points.Add(new GesturePoint(x, y));
            }
        }

        return points;
    }

    private static bool TryReadCoordinate(JsonElement point, string member, out double value)
    {
        value = 0;
        return point.ValueKind == JsonValueKind.Object
            && point.TryGetProperty(member, out var coordinate)
            && coordinate.ValueKind == JsonValueKind.Number
            && coordinate.TryGetDouble(out value);
    }
}
