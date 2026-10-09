using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Config;

/// <summary>
/// The canonical text of one sync item (<c>Sync/SyncItem</c>), written by the same writers as the config
/// file (the gesture serializer of <see cref="ConfigJsonContext"/>, <see cref="MappingJsonWriter"/>) so two
/// items are equal exactly when their text is, and read back by the same readers when a stored text has to
/// become an item again (resolving a conflict). One line of compact JSON, except that a gesture's samples keep
/// the config file's one-sample-per-line layout. A command and a category carry their group's id beside them:
/// <c>{ "group": "&lt;id&gt;", "command": { … } }</c>, <c>{ "group": "&lt;id&gt;", "id": "…", "name": "…", "useOn": [ … ] }</c>
/// (<c>useOn</c> only when not every platform); a hold remap likewise:
/// <c>{ "group": "&lt;id&gt;", "id": "…", "name": "…", "holdKey": "Space", "tapTimeMs": 180, "isActive": true }</c>.
/// A group is its header only (no commands, categories or hold remaps): those are items of their own.
/// </summary>
internal static class SyncItemJson
{
    private static readonly JsonDocumentOptions ParseOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public static string Gesture(Gesture gesture)
        => Write(writer => JsonSerializer.Serialize(writer, gesture, ConfigJsonContext.Default.Gesture));

    public static string GroupHeader(AppGroup group)
        => Write(writer => MappingJsonWriter.WriteGroup(writer, group with { Commands = [], Categories = [], HoldRemaps = [] }));

    /// <summary><c>{ "group", "id", "name" }</c>, plus <c>useOn</c> when the category is not on every platform (sync format 3).</summary>
    public static string Category(GroupId groupId, CommandCategory category)
        => Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("group", groupId.Value);
            MappingJsonWriter.WriteCategoryMembers(writer, category);
            writer.WriteEndObject();
        });

    /// <summary>A hold remap with its group's id (sync format 11): <c>{ "group", "id", "name", "holdKey", "tapTimeMs", "isActive" }</c>, plus <c>useOn</c> when set.</summary>
    public static string HoldRemap(GroupId groupId, HoldRemap holdRemap)
        => Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("group", groupId.Value);
            MappingJsonWriter.WriteHoldRemapMembers(writer, holdRemap);
            writer.WriteEndObject();
        });

    public static string Command(GroupId groupId, Command command)
        => Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("group", groupId.Value);
            writer.WritePropertyName("command");
            MappingJsonWriter.WriteCommand(writer, command);
            writer.WriteEndObject();
        });

    public static string Ignored(IgnoredApp app) => Write(writer => MappingJsonWriter.WriteIgnored(writer, app));

    /// <summary>A command's own version with the command's id: <c>{ "command": "&lt;id&gt;", "version": { … } }</c>.</summary>
    public static string Version(CommandId commandId, CommandVersion version)
        => Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("command", commandId.Value);
            writer.WritePropertyName("version");
            MappingJsonWriter.WriteOwnVersion(writer, version);
            writer.WriteEndObject();
        });

    public static (CommandId Command, CommandVersion Version) ReadVersion(string content, StepRegistry steps)
    {
        var item = Parse(content, "An own-steps item");
        var command = new CommandId(JsonMembers.RequireGuid(item, "command", "an own-steps item"));
        return (command, new MappingJsonReader(steps, notice: null).ReadOwnVersion(item["version"], "the steps of an own-steps item"));
    }

    /// <exception cref="ConfigFormatException">The text is not a gesture.</exception>
    public static Gesture ReadGesture(string content)
    {
        try
        {
            var gesture = JsonSerializer.Deserialize(content, ConfigJsonContext.Default.Gesture)
                ?? throw new ConfigFormatException("A gesture item is empty.");
            ConfigSerializer.EnsureComplete([gesture]);
            return gesture;
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException($"A gesture item could not be read: {ex.Message}", ex);
        }
    }

    public static AppGroup ReadGroupHeader(string content, StepRegistry steps)
        => new MappingJsonReader(steps, notice: null).ReadGroup(Parse(content, "An app group item"));

    public static (GroupId Group, CommandCategory Category) ReadCategory(string content)
    {
        var item = Parse(content, "A category item");
        const string where = "a category item";
        var group = new GroupId(JsonMembers.RequireGuid(item, "group", where));
        var category = new CommandCategory(new CategoryId(JsonMembers.RequireGuid(item, "id", where)), JsonMembers.RequireString(item, "name", where))
        {
            UseOn = MappingJsonReader.ReadUseOn(item, where),
        };
        return (group, category);
    }

    /// <exception cref="ConfigFormatException">The text is not a hold remap item.</exception>
    public static (GroupId Group, HoldRemap HoldRemap) ReadHoldRemap(string content)
    {
        var item = Parse(content, "A hold remap item");
        const string where = "a hold remap item";
        var group = new GroupId(JsonMembers.RequireGuid(item, "group", where));
        return (group, MappingJsonReader.ReadHoldRemap(item, new HoldRemapId(JsonMembers.RequireGuid(item, "id", where)), where));
    }

    /// <summary>A step its type cannot read is dropped (as on load); the caller compares the result with what it expected.</summary>
    public static (GroupId Group, Command Command) ReadCommand(string content, StepRegistry steps)
    {
        var item = Parse(content, "A command item");
        var group = new GroupId(JsonMembers.RequireGuid(item, "group", "a command item"));
        return (group, new MappingJsonReader(steps, notice: null).ReadCommand(item["command"], group.ToString()));
    }

    public static IgnoredApp ReadIgnored(string content) => MappingJsonReader.ReadIgnored(Parse(content, "An ignored app item"));

    private static JsonObject Parse(string content, string what)
    {
        try
        {
            return JsonMembers.RequireObject(JsonNode.Parse(content, documentOptions: ParseOptions), what);
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException($"{what} is not valid JSON: {ex.Message}", ex);
        }
    }

    private static string Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
