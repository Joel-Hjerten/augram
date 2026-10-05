using System.Text.Json;

namespace Augram.Import.StrokesPlus;

/// <summary>Counts gestures, samples, actions and applications without importing them.</summary>
internal static class SourceStatsReader
{
    public static SourceStats Read(JsonElement root)
    {
        var gestureCount = 0;
        var sampleCount = 0;
        if (root.TryGetProperty(StrokesPlusJson.Gestures, out var gestures) && gestures.ValueKind == JsonValueKind.Array)
        {
            gestureCount = gestures.GetArrayLength();
            sampleCount = gestures.EnumerateArray().Sum(CountPatterns);
        }

        int? actionCount = null;
        if (root.TryGetProperty(StrokesPlusJson.GlobalApplication, out var global) && global.ValueKind == JsonValueKind.Object)
        {
            actionCount = CountActions(global);
        }

        int? applicationCount = null;
        if (root.TryGetProperty(StrokesPlusJson.Applications, out var applications) && applications.ValueKind == JsonValueKind.Array)
        {
            applicationCount = applications.GetArrayLength();
            actionCount = (actionCount ?? 0) + applications.EnumerateArray().Sum(CountActions);
        }

        return new SourceStats(gestureCount, sampleCount, actionCount, applicationCount);
    }

    private static int CountPatterns(JsonElement gesture) => ArrayLength(gesture, StrokesPlusJson.Gesture.PointPatterns);

    private static int CountActions(JsonElement application) => ArrayLength(application, StrokesPlusJson.Actions);

    private static int ArrayLength(JsonElement element, string member)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(member, out var array)
            && array.ValueKind == JsonValueKind.Array
            ? array.GetArrayLength()
            : 0;
    }
}
