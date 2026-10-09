using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;

namespace Augram.Core.Config;

/// <summary>
/// The hold remap members of the file (F9, schema 4) as <see cref="MappingJsonWriter"/> writes them, read like categories: a
/// bad entry never fails a load. A hold remap that is not an object, has no Guid <c>id</c>, or a member of the wrong shape
/// (an unknown <c>holdKey</c>, a <c>tapTimeMs</c> that is not an integer) is dropped with a notice; a missing name, tap time,
/// active flag or <c>useOn</c> takes its default. A command's <c>holdRemap</c> that is not a Guid string reads as none with a
/// notice; one naming no hold remap of its group is left for <see cref="HoldRemapRules"/>, which makes the command ordinary.
/// An <c>input</c> of the wrong shape is a format error, like any trigger's.
/// </summary>
internal sealed partial class MappingJsonReader
{
    private List<HoldRemap> ReadHoldRemaps(JsonArray array, string where)
    {
        var holdRemaps = new List<HoldRemap>(array.Count);
        for (int i = 0; i < array.Count; i++)
        {
            var what = $"Hold remap {i + 1} of {where}";
            if (array[i] is not JsonObject item)
            {
                _notice?.Invoke($"{what} dropped: it is not a JSON object.");
            }
            else if (JsonMembers.TryGuid(item, "id") is not { } id)
            {
                _notice?.Invoke($"{what} dropped: 'id' is missing or not a Guid string.");
            }
            else
            {
                try
                {
                    holdRemaps.Add(ReadHoldRemap(item, new HoldRemapId(id), "the hold remap"));
                }
                catch (ConfigFormatException ex)
                {
                    _notice?.Invoke($"{what} dropped: {ex.Message}");
                }
            }
        }

        return holdRemaps;
    }

    /// <summary>One hold remap's members after its id (the sync item reads the same).</summary>
    /// <exception cref="ConfigFormatException">A member has the wrong shape.</exception>
    public static HoldRemap ReadHoldRemap(JsonObject item, HoldRemapId id, string where)
        => new(id, JsonMembers.OptionalString(item, "name", where) ?? string.Empty, JsonMembers.OptionalEnum(item, "holdKey", KeyCode.None, where))
        {
            TapTimeMs = JsonMembers.OptionalInt32(item, "tapTimeMs", HoldRemap.DefaultTapTimeMs, where),
            IsActive = JsonMembers.OptionalBool(item, "isActive", fallback: true, where),
            UseOn = ReadUseOn(item, where),
        };

    /// <summary>Missing or null: an ordinary command. Not a Guid string: an ordinary command, with a notice.</summary>
    private HoldRemapId? ReadHoldRemapReference(JsonObject command, string where)
    {
        if (command["holdRemap"] is null)
        {
            return null;
        }

        if (JsonMembers.TryGuid(command, "holdRemap") is { } id)
        {
            return new HoldRemapId(id);
        }

        _notice?.Invoke($"The hold remap of {where} dropped: 'holdRemap' must be a Guid string; the command is an ordinary one.");
        return null;
    }

    /// <summary>An input trigger's <c>input</c>: <c>{ "buttons": "Left, Right" }</c>, <c>{ "wheel": "Up" }</c> or <c>{ "key": "W" }</c>.</summary>
    private static HoldInput ReadInput(JsonNode? node, string what)
    {
        var where = $"'input' of {what}";
        var input = JsonMembers.RequireObject(node, where);
        if (input["buttons"] is not null)
        {
            return new HoldInput.Buttons(JsonMembers.OptionalFlags(input, "buttons", HeldButtons.None, where));
        }

        if (input["wheel"] is not null)
        {
            return new HoldInput.Wheel(JsonMembers.OptionalEnum(input, "wheel", WheelDirection.Up, where));
        }

        if (input["key"] is not null)
        {
            return new HoldInput.Key(JsonMembers.OptionalEnum(input, "key", KeyCode.None, where));
        }

        throw new ConfigFormatException($"{where} must be {{ \"buttons\": \"Left, Right\" }}, {{ \"wheel\": \"Up\" | \"Down\" }} or {{ \"key\": \"W\" }}.");
    }
}
