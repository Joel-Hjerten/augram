using System.Text.Json;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

/// <summary>
/// An Exclusions › While holding entry's inputs (schema 9, plan 0012), written by <see cref="WriteIgnored"/> after
/// <c>disableEntirely</c>: <c>holding</c>, the original (only when the entry has inputs on some side), and <c>ownHolding</c>,
/// the own version (only when there is one). Each input is <c>{ "key": "S" }</c> (a <c>KeyCode</c> name), <c>{ "modifier":
/// "Alt" }</c> (a <c>KeyModifiers</c> name: <c>Control</c>, <c>Alt</c>, <c>Shift</c>, <c>Meta</c>) or <c>{ "button": "Middle" }</c>
/// (a <c>MouseButton</c> name), with <c>authoredOn</c>. An entry on the other lists writes neither, so its file and sync item
/// read as before.
/// </summary>
internal static partial class MappingJsonWriter
{
    private static void WriteHolding(Utf8JsonWriter writer, PlatformList<WhileHoldingInput> holding)
    {
        if (holding.IsEmpty)
        {
            return;
        }

        WriteHeldInputs(writer, "holding", holding.Original);
        if (holding.Own is { } own)
        {
            writer.WriteStartObject("ownHolding");
            writer.WriteString("platform", own.Platform.ToString());
            WriteHeldInputs(writer, "inputs", own.Items);
            writer.WriteEndObject();
        }
    }

    private static void WriteHeldInputs(Utf8JsonWriter writer, string name, IReadOnlyList<WhileHoldingInput> inputs)
    {
        writer.WriteStartArray(name);
        foreach (var row in inputs)
        {
            writer.WriteStartObject();
            switch (row.Input)
            {
                case KeyOrButton.Key key:
                    writer.WriteString("key", key.KeyCode.ToString());
                    break;
                case KeyOrButton.Modifier modifier:
                    writer.WriteString("modifier", modifier.Modifiers.ToString());
                    break;
                case KeyOrButton.Button button:
                    writer.WriteString("button", button.MouseButton.ToString());
                    break;
            }

            writer.WriteString("authoredOn", row.AuthoredOn.ToString());
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
