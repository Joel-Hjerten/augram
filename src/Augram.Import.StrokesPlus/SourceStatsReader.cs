using System.Text.Json;

namespace Augram.Import.StrokesPlus;

/// <summary>Counts gestures, samples, actions, steps, applications and ignored applications without importing them.</summary>
internal static class SourceStatsReader
{
    public static SourceStats Read(JsonElement root)
    {
        var gestureCount = 0;
        var sampleCount = 0;
        if (JsonRead.TryArray(root, StrokesPlusJson.Gestures, out var gestures))
        {
            gestureCount = gestures.GetArrayLength();
            sampleCount = gestures.EnumerateArray().Sum(CountPatterns);
        }

        int? actionCount = null;
        int? stepCount = null;
        if (JsonRead.TryObject(root, StrokesPlusJson.GlobalApplication, out var global))
        {
            actionCount = CountActions(global);
            stepCount = CountSteps(global);
        }

        int? applicationCount = null;
        if (JsonRead.TryArray(root, StrokesPlusJson.Applications, out var applications))
        {
            applicationCount = applications.GetArrayLength();
            actionCount = (actionCount ?? 0) + applications.EnumerateArray().Sum(CountActions);
            stepCount = (stepCount ?? 0) + applications.EnumerateArray().Sum(CountSteps);
        }

        int? ignoredCount = null;
        if (JsonRead.TryArray(root, StrokesPlusJson.IgnoredApplications, out var ignored))
        {
            ignoredCount = ignored.GetArrayLength();
        }

        return new SourceStats(gestureCount, sampleCount, actionCount, applicationCount, stepCount, ignoredCount);
    }

    private static int CountPatterns(JsonElement gesture) => ArrayLength(gesture, StrokesPlusJson.Gesture.PointPatterns);

    private static int CountActions(JsonElement application) => ArrayLength(application, StrokesPlusJson.Actions);

    private static int CountSteps(JsonElement application)
        => JsonRead.TryArray(application, StrokesPlusJson.Actions, out var actions)
            ? actions.EnumerateArray().Sum(action => ArrayLength(action, StrokesPlusJson.Action.Steps))
            : 0;

    private static int ArrayLength(JsonElement element, string member)
        => JsonRead.TryArray(element, member, out var array) ? array.GetArrayLength() : 0;
}
