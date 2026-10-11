using System.Text.Json;

namespace Augram.Import.StrokesPlus;

/// <summary>Counts the gestures in the source without importing them.</summary>
internal static class SourceStatsReader
{
    public static SourceStats Read(JsonElement root)
        => new(JsonRead.TryArray(root, StrokesPlusJson.Gestures, out var gestures) ? gestures.GetArrayLength() : 0);
}
