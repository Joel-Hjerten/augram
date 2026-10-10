using System.Globalization;
using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
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
/// passes and report the rest. Steps go through <see cref="CommandStepJsonReader"/>. A bad category
/// entry never fails a load: a category without a Guid <c>id</c> or a name is dropped with a notice, one
/// whose <c>useOn</c> is not a list of strings is used on every platform with a notice, a command
/// <c>category</c> that is not a Guid string reads as null with a notice, and one that names no category of
/// its group is left for <see cref="CategoryRules"/> to clear silently. A command's <c>notIn</c> (schema 5; ignored-app ids
/// since schema 6) and <c>alsoIn</c> (schema 7, plan 0005) likewise: an entry that is not a Guid string is dropped with a notice,
/// one naming no Ignored › Per command entry (for <c>notIn</c>) or no plain Exclusions › Global entry (for <c>alsoIn</c>) is left
/// for <see cref="MappingRules"/>. An ignored app's <c>scope</c> (schema 6) is an enum member like any other.
/// Triggers are read in <c>MappingJsonReader.Triggers.cs</c>, hold remaps in <c>MappingJsonReader.HoldRemaps.cs</c>.
/// </summary>
internal sealed partial class MappingJsonReader
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
            HoldRemaps = ReadHoldRemaps(JsonMembers.OptionalArray(group, "holdRemaps", where), where),
        };
    }

    /// <summary>
    /// F8 "Use on" of a group, a category or a command: absent is every platform; target names this version does not know (a
    /// later machine target) are passed over.
    /// </summary>
    public static PlatformSet ReadUseOn(JsonObject owner, string where)
    {
        if (owner["useOn"] is null)
        {
            return PlatformSet.All;
        }

        var set = PlatformSet.None;
        foreach (var target in JsonMembers.OptionalStrings(owner, "useOn", where))
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
                categories.Add(new CommandCategory(new CategoryId(id), name) { UseOn = ReadCategoryUseOn(category, $"Category '{name}' of {where}") });
            }
        }

        return categories;
    }

    /// <summary>A category's "Use on" (F8, 2026-10-08); one that is not a list of strings never fails a load: every platform, with a notice.</summary>
    private PlatformSet ReadCategoryUseOn(JsonObject category, string what)
    {
        try
        {
            return ReadUseOn(category, what);
        }
        catch (ConfigFormatException)
        {
            _notice?.Invoke($"{what}: 'useOn' must be a list of platform names; the category is used on every platform.");
            return PlatformSet.All;
        }
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
            ReadCategoryReference(command, where))
        {
            UseOn = ReadUseOn(command, where),
            NotIn = ReadIgnoredIds(command, "notIn", where),
            AlsoIn = ReadIgnoredIds(command, "alsoIn", where),
            OwnVersion = ReadOwnVersion(command, where),
            HoldRemapId = ReadHoldRemapReference(command, where),
        };
    }

    /// <summary>F8: the steps of the platform the command was not authored on; absent or null is none.</summary>
    private CommandVersion? ReadOwnVersion(JsonObject command, string where)
    {
        if (command["ownVersion"] is null)
        {
            return null;
        }

        return ReadOwnVersion(command["ownVersion"], $"the own version of {where}");
    }

    /// <summary>An own version object as <see cref="MappingJsonWriter.WriteOwnVersion"/> writes it.</summary>
    public CommandVersion ReadOwnVersion(JsonNode? node, string what)
    {
        var own = JsonMembers.RequireObject(node, what);
        var changedAt = JsonMembers.OptionalString(own, "changedAt", what);
        return new CommandVersion(
            JsonMembers.OptionalEnum(own, "platform", HostPlatform.MacOS, what),
            _steps.ReadAll(JsonMembers.OptionalArray(own, "steps", what), what),
            JsonMembers.OptionalString(own, "basedOn", what) ?? string.Empty,
            DateTimeOffset.TryParse(changedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : DateTimeOffset.UnixEpoch)
        {
            // Absent: the platform uses the original's trigger, converted; present (null included): its own.
            Trigger = own.ContainsKey("trigger") ? ReadTrigger(own["trigger"], what) : null,
        };
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

    /// <summary>
    /// A command's list of ignored-app ids under <paramref name="name"/>: its "Not in" (<c>notIn</c>, plan 0004; schema 6: the
    /// ids of Ignored › Per command entries) or its "Also in" (<c>alsoIn</c>, plan 0005, schema 7: the ids of Exclusions › Global
    /// entries). Missing or null is none; not an array of strings is a format error; an entry that is not a Guid string is
    /// dropped with a notice. Ids naming no entry of the right kind (an app group's, from a schema 5 <c>notIn</c>, a deleted
    /// entry's, one in the other list or, for <c>alsoIn</c>, one that disables Augram while focused) are left for
    /// <see cref="MappingRules"/>, which drops them silently (and the whole list under a hold remap, and an "Also in" on a command
    /// whose trigger holds the stroke button on both platforms).
    /// </summary>
    private List<GroupId> ReadIgnoredIds(JsonObject command, string name, string where)
    {
        var entries = JsonMembers.OptionalStrings(command, name, where);
        var apps = new List<GroupId>(entries.Count);
        foreach (var entry in entries)
        {
            if (Guid.TryParse(entry, out var id))
            {
                apps.Add(new GroupId(id));
            }
            else
            {
                _notice?.Invoke($"An entry of '{name}' of {where} dropped: \"{entry}\" is not a Guid string.");
            }
        }

        return apps;
    }

    /// <summary>An ignored app; its <c>scope</c> (schema 6, plan 0004) missing or null is Ignored › Global, as before.</summary>
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
            JsonMembers.OptionalBool(app, "disableEntirely", fallback: false, where))
        {
            Scope = JsonMembers.OptionalEnum(app, "scope", IgnoreScope.Global, where),
        };
    }
}
