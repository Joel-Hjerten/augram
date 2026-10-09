using System.Text.Json;
using Augram.Core.HoldRemaps;

namespace Augram.Core.Config;

/// <summary>
/// The hold remap members of the file (F9, schema 4): a group's <c>holdRemaps</c> (omitted when empty), each
/// <c>{ "id", "name", "holdKey", "tapTimeMs", "isActive" }</c> plus <c>useOn</c> when not every platform; a command's
/// <c>holdRemap</c> (written by <see cref="WriteCommand"/>); and an input trigger's <c>input</c>:
/// <c>{ "buttons": "Left, Right" }</c>, <c>{ "wheel": "Up" }</c> or <c>{ "key": "W" }</c>.
/// </summary>
internal static partial class MappingJsonWriter
{
    /// <summary>A hold remap's own members; the sync item writes the same after its group.</summary>
    public static void WriteHoldRemapMembers(Utf8JsonWriter writer, HoldRemap holdRemap)
    {
        writer.WriteString("id", holdRemap.Id.Value);
        writer.WriteString("name", holdRemap.Name);
        writer.WriteString("holdKey", holdRemap.HoldKey.ToString());
        writer.WriteNumber("tapTimeMs", holdRemap.TapTimeMs);
        writer.WriteBoolean("isActive", holdRemap.IsActive);
        WriteUseOn(writer, holdRemap.UseOn);
    }

    private static void WriteHoldRemaps(Utf8JsonWriter writer, IReadOnlyList<HoldRemap> holdRemaps)
    {
        if (holdRemaps.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("holdRemaps");
        foreach (var holdRemap in holdRemaps)
        {
            writer.WriteStartObject();
            WriteHoldRemapMembers(writer, holdRemap);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>The <c>input</c> member of an input trigger, inside the trigger object.</summary>
    private static void WriteInput(Utf8JsonWriter writer, HoldInput input)
    {
        writer.WriteStartObject("input");
        switch (input)
        {
            case HoldInput.Buttons buttons:
                writer.WriteString("buttons", buttons.Set.ToString());
                break;
            case HoldInput.Wheel wheel:
                writer.WriteString("wheel", wheel.Direction.ToString());
                break;
            case HoldInput.Key key:
                writer.WriteString("key", key.KeyCode.ToString());
                break;
        }

        writer.WriteEndObject();
    }
}
