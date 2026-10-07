using System.Text.Json.Nodes;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Config;

/// <summary>
/// Reads the <c>mapping</c> member of the file as <see cref="MappingJsonWriter"/> writes it, and as a
/// hand edit might leave it: a missing or null member is <see cref="MappingDocument.Empty"/> (a file
/// written before M2 still loads), a missing <c>groups</c> array means just the Global group, and
/// every optional member takes its default. Structure only: ids and names must be there and have the
/// right shape (a <see cref="ConfigFormatException"/> otherwise), but the rules (unique names, one
/// command per trigger, a Global group) are the store's, so <see cref="ConfigSession"/> can keep what
/// passes and report the rest. Steps go through <see cref="CommandStepJsonReader"/>. Categories are
/// cosmetic, so a bad entry never fails a load: a category without a Guid <c>id</c> or a name is dropped
/// with a notice, a command <c>category</c> that is not a Guid string reads as null with a notice, and
/// one that names no category of its group is left for <see cref="CategoryRules"/> to clear silently.
/// </summary>
internal sealed class MappingJsonReader
{
    private readonly CommandStepJsonReader _steps;
    private readonly Action<string>? _notice;

    public MappingJsonReader(StepRegistry registry, Action<string>? notice)
    {
        _steps = new CommandStepJsonReader(registry, notice);
        _notice = notice;
    }

    public MappingDocument Read(JsonNode? node)
    {
        if (node is null)
        {
            return MappingDocument.Empty;
        }

        var root = JsonMembers.RequireObject(node, "'mapping'");
        var groups = root["groups"] is null
            ? MappingDocument.Empty.Groups
            : JsonMembers.OptionalArray(root, "groups", "'mapping'").Select(ReadGroup).ToArray();
        var ignored = JsonMembers.OptionalArray(root, "ignored", "'mapping'").Select(ReadIgnored).ToArray();
        return new MappingDocument(groups, ignored);
    }

    public AppGroup ReadGroup(JsonNode? node)
    {
        var group = JsonMembers.RequireObject(node, "An app group");
        var name = JsonMembers.RequireString(group, "name", "an app group");
        var where = $"app group '{name}'";
        var matcher = group["matcher"] is null ? null : ReadMatcher(group["matcher"], where);
        var categories = ReadCategories(JsonMembers.OptionalArray(group, "categories", where), where);
        var commands = JsonMembers.OptionalArray(group, "commands", where)
            .Select(command => ReadCommand(command, name))
            .ToArray();

        return new AppGroup(
            new GroupId(JsonMembers.RequireGuid(group, "id", where)),
            name,
            JsonMembers.OptionalBool(group, "isActive", fallback: true, where),
            JsonMembers.OptionalBool(group, "suppressGlobals", fallback: false, where),
            matcher,
            commands,
            categories)
        {
            UseOn = ReadUseOn(group, where),
        };
    }

    /// <summary>F8 "Use on": absent is every platform; target names this version does not know (a later machine target) are passed over.</summary>
    private static PlatformSet ReadUseOn(JsonObject group, string where)
    {
        if (group["useOn"] is null)
        {
            return PlatformSet.All;
        }

        var set = PlatformSet.None;
        foreach (var target in JsonMembers.OptionalStrings(group, "useOn", where))
        {
            if (string.Equals(target, MappingJsonWriter.UseOnWindows, StringComparison.OrdinalIgnoreCase))
            {
                set |= PlatformSet.Windows;
            }
            else if (string.Equals(target, MappingJsonWriter.UseOnMacOS, StringComparison.OrdinalIgnoreCase))
            {
                set |= PlatformSet.MacOS;
            }
        }

        return set;
    }

    private List<CommandCategory> ReadCategories(JsonArray array, string where)
    {
        var categories = new List<CommandCategory>(array.Count);
        for (int i = 0; i < array.Count; i++)
        {
            var what = $"Category {i + 1} of {where}";
            if (array[i] is not JsonObject category)
            {
                _notice?.Invoke($"{what} dropped: it is not a JSON object.");
            }
            else if (JsonMembers.TryGuid(category, "id") is not { } id)
            {
                _notice?.Invoke($"{what} dropped: 'id' is missing or not a Guid string.");
            }
            else if (JsonMembers.TryString(category, "name") is not { } name || string.IsNullOrWhiteSpace(name))
            {
                _notice?.Invoke($"{what} dropped: it has no name.");
            }
            else
            {
                categories.Add(new CommandCategory(new CategoryId(id), name));
            }
        }

        return categories;
    }

    public Command ReadCommand(JsonNode? node, string groupName)
    {
        var command = JsonMembers.RequireObject(node, $"A command in '{groupName}'");
        var name = JsonMembers.RequireString(command, "name", $"a command in '{groupName}'");
        var where = $"command '{name}' in '{groupName}'";

        return new Command(
            new CommandId(JsonMembers.RequireGuid(command, "id", where)),
            name,
            ReadTrigger(command["trigger"], where),
            JsonMembers.OptionalBool(command, "isActive", fallback: true, where),
            _steps.ReadAll(JsonMembers.OptionalArray(command, "steps", where), where),
            JsonMembers.OptionalString(command, "note", where),
            ReadCategoryReference(command, where));
    }

    /// <summary>Missing or null: Uncategorized. Not a Guid string: Uncategorized, with a notice.</summary>
    private CategoryId? ReadCategoryReference(JsonObject command, string where)
    {
        if (command["category"] is null)
        {
            return null;
        }

        if (JsonMembers.TryGuid(command, "category") is { } id)
        {
            return new CategoryId(id);
        }

        _notice?.Invoke($"The category of {where} dropped: 'category' must be a Guid string; the command is uncategorized.");
        return null;
    }

    private static Trigger ReadTrigger(JsonNode? node, string where)
    {
        if (node is null)
        {
            return Trigger.None;
        }

        var what = $"'trigger' of {where}";
        var trigger = JsonMembers.RequireObject(node, what);
        if (trigger["gesture"] is not null)
        {
            return Trigger.ForGesture(new GestureId(JsonMembers.RequireGuid(trigger, "gesture", what)));
        }

        if (trigger["wheel"] is not null)
        {
            return Trigger.ForWheel(JsonMembers.OptionalEnum(trigger, "wheel", WheelDirection.Up, what));
        }

        throw new ConfigFormatException($"{what} must be null, {{ \"gesture\": \"<id>\" }} or {{ \"wheel\": \"Up\" | \"Down\" }}.");
    }

    private static AppMatcher ReadMatcher(JsonNode? node, string where)
    {
        var what = $"'matcher' of {where}";
        var matcher = JsonMembers.RequireObject(node, what);
        return new AppMatcher
        {
            WindowsProcessNames = JsonMembers.OptionalStrings(matcher, "processNames", what),
            MacProcessNames = JsonMembers.OptionalStrings(matcher, "macProcessNames", what),
            ProcessPath = JsonMembers.OptionalString(matcher, "processPath", what),
            ProcessPathIsRegex = JsonMembers.OptionalBool(matcher, "processPathIsRegex", fallback: false, what),
            Title = JsonMembers.OptionalString(matcher, "title", what),
            TitleIsRegex = JsonMembers.OptionalBool(matcher, "titleIsRegex", fallback: false, what),
            ClassChain = JsonMembers.OptionalStrings(matcher, "classChain", what),
            IgnoreWhenFullScreen = JsonMembers.OptionalBool(matcher, "ignoreWhenFullScreen", fallback: false, what),
        };
    }

    public static IgnoredApp ReadIgnored(JsonNode? node)
    {
        var app = JsonMembers.RequireObject(node, "An ignored app");
        var name = JsonMembers.RequireString(app, "name", "an ignored app");
        var where = $"ignored app '{name}'";

        return new IgnoredApp(
            new GroupId(JsonMembers.RequireGuid(app, "id", where)),
            name,
            JsonMembers.OptionalBool(app, "isActive", fallback: true, where),
            app["matcher"] is null ? AppMatcher.Empty : ReadMatcher(app["matcher"], where),
            JsonMembers.OptionalBool(app, "disableEntirely", fallback: false, where));
    }
}
