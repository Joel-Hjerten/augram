using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

/// <summary>
/// An Exclusions › While holding entry's inputs (schema 9, plan 0012) as <see cref="MappingJsonWriter"/> writes them: the
/// entry's <c>holding</c> (the original, <c>[{ "key": "S", "authoredOn": "Windows" }, { "modifier": "Alt", … }, { "button":
/// "Middle", … }]</c>) and <c>ownHolding</c> (<c>{ "platform": "MacOS", "inputs": [ … ] }</c>, the own version). Missing or null
/// is none. An input of the wrong shape (none of the three members, a name its enum lacks) is a format error, like a trigger's;
/// the rules then fold a modifier key into its modifier and drop invalid and repeated inputs.
/// </summary>
internal sealed partial class MappingJsonReader
{
    private static PlatformList<WhileHoldingInput> ReadHolding(JsonObject app, string where)
    {
        if (app["holding"] is null && app["ownHolding"] is null)
        {
            return PlatformList<WhileHoldingInput>.Empty;
        }

        var original = ReadHeldInputs(JsonMembers.OptionalArray(app, "holding", where), $"'holding' of {where}");
        PlatformVersion<WhileHoldingInput>? own = null;
        if (app["ownHolding"] is not null)
        {
            var what = $"'ownHolding' of {where}";
            var version = JsonMembers.RequireObject(app["ownHolding"], what);
            own = new PlatformVersion<WhileHoldingInput>(
                JsonMembers.OptionalEnum(version, "platform", HostPlatform.MacOS, what),
                ReadHeldInputs(JsonMembers.OptionalArray(version, "inputs", what), what));
        }

        return new PlatformList<WhileHoldingInput>(original, own);
    }

    private static WhileHoldingInput[] ReadHeldInputs(JsonArray array, string what)
    {
        var inputs = new WhileHoldingInput[array.Count];
        for (int i = 0; i < array.Count; i++)
        {
            var where = $"input {i + 1} of {what}";
            var item = JsonMembers.RequireObject(array[i], where);
            inputs[i] = new WhileHoldingInput(ReadKeyOrButton(item, where), JsonMembers.OptionalEnum(item, "authoredOn", HostPlatform.Windows, where));
        }

        return inputs;
    }

    private static KeyOrButton ReadKeyOrButton(JsonObject item, string where)
    {
        if (item["key"] is not null)
        {
            return new KeyOrButton.Key(JsonMembers.OptionalEnum(item, "key", KeyCode.None, where));
        }

        if (item["modifier"] is not null)
        {
            return new KeyOrButton.Modifier(JsonMembers.OptionalFlags(item, "modifier", KeyModifiers.None, where));
        }

        if (item["button"] is not null)
        {
            return new KeyOrButton.Button(JsonMembers.OptionalEnum(item, "button", MouseButton.Left, where));
        }

        throw new ConfigFormatException($"{where} must be {{ \"key\": \"S\" }}, {{ \"modifier\": \"Alt\" }} or {{ \"button\": \"Middle\" }}.");
    }
}
