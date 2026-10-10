using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

/// <summary>
/// A trigger as <see cref="MappingJsonWriter"/> writes it, a command's and an own version's alike: null, an input (schema 4),
/// or a gesture, wheel, click or button object with an optional <c>hold</c> (schema 2). A button trigger (schema 7, plan 0005)
/// is <c>{ "button": "Left", "hold": { "buttons": "Right" } }</c>, the pressed button by its <see cref="MouseButton"/> name; one
/// without a <c>hold</c> holds the stroke button, which <see cref="MappingRules"/> refuses. A member of the wrong shape, a
/// button name the enum lacks among them, is a format error naming it. The hold's <c>dragDistancePx</c> (schema 5, plan 0004)
/// must be an integer; one outside 1 to
/// <see cref="TriggerHold.MaxDragDistancePx"/> never fails a load: it is dropped with a notice and the command uses the
/// Options value, as an out-of-range setting falls back to its default.
/// </summary>
internal sealed partial class MappingJsonReader
{
    private Trigger ReadTrigger(JsonNode? node, string where)
    {
        if (node is null)
        {
            return Trigger.None;
        }

        var what = $"'trigger' of {where}";
        var trigger = JsonMembers.RequireObject(node, what);
        if (trigger["input"] is not null)
        {
            return Trigger.ForInput(ReadInput(trigger["input"], what));
        }

        var hold = ReadHold(trigger, what);
        if (trigger["gesture"] is not null)
        {
            return Trigger.ForGesture(new GestureId(JsonMembers.RequireGuid(trigger, "gesture", what)), hold);
        }

        if (trigger["wheel"] is not null)
        {
            return Trigger.ForWheel(JsonMembers.OptionalEnum(trigger, "wheel", WheelDirection.Up, what), hold);
        }

        if (trigger["button"] is not null)
        {
            return Trigger.ForButton(JsonMembers.OptionalEnum(trigger, "button", MouseButton.Left, what), hold);
        }

        if (JsonMembers.OptionalBool(trigger, "click", fallback: false, what))
        {
            return Trigger.ForClick(hold);
        }

        throw new ConfigFormatException($"{what} must be null, {{ \"gesture\": \"<id>\" }}, {{ \"wheel\": \"Up\" | \"Down\" }}, {{ \"click\": true }} or {{ \"button\": \"Left\" }}, each with an optional \"hold\", or {{ \"input\": {{ … }} }}.");
    }

    /// <summary>F1 combinations (schema 2): the "while holding" set; missing or null is the stroke button alone.</summary>
    private TriggerHold ReadHold(JsonObject trigger, string what)
    {
        if (trigger["hold"] is null)
        {
            return TriggerHold.Default;
        }

        var where = $"'hold' of {what}";
        var hold = JsonMembers.RequireObject(trigger["hold"], where);
        return new TriggerHold(
            JsonMembers.OptionalFlags(hold, "buttons", HeldButtons.Stroke, where),
            JsonMembers.OptionalFlags(hold, "keys", KeyModifiers.None, where),
            JsonMembers.OptionalEnum(hold, "capture", HoldCapture.Either, where),
            ReadDragDistance(hold, where));
    }

    /// <summary>A command's own drag distance (schema 5): missing or null is the Options value; out of range, the Options value with a notice.</summary>
    private int? ReadDragDistance(JsonObject hold, string where)
    {
        var distance = JsonMembers.OptionalInt32(hold, "dragDistancePx", where);
        if (distance is < 1 or > TriggerHold.MaxDragDistancePx)
        {
            _notice?.Invoke($"'dragDistancePx' of {where} dropped: {distance} is not between 1 and {TriggerHold.MaxDragDistancePx}; the Options value is used.");
            return null;
        }

        return distance;
    }
}
