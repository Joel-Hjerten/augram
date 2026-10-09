using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Sync;

/// <summary>
/// One mergeable unit of the synced data (README: items), a closed set like <c>Trigger</c>: a gesture, an
/// app group's header, a category with its group, a hold remap with its group (F9), a command with its group (without its
/// own version), a command's own version (F8), an ignored app. Each carries its
/// model value and its canonical <see cref="Content"/>: the item's JSON as the config file writes it, so two
/// items are the same exactly when their contents are equal. Compare <see cref="Content"/>, never the
/// objects. Immutable.
/// </summary>
public abstract class SyncItem
{
    private SyncItem(SyncItemKey key, string name, string content)
    {
        Key = key;
        Name = name;
        Content = content;
    }

    public SyncItemKey Key { get; }

    public SyncItemKind Kind => Key.Kind;

    /// <summary>The name a person knows it by, for conflict lists and repair lines.</summary>
    public string Name { get; }

    /// <summary>The canonical JSON text; equal text is equal item.</summary>
    public string Content { get; }

    /// <summary>Turns a stored <see cref="Content"/> back into an item (resolving a conflict).</summary>
    /// <exception cref="ConfigFormatException">The text is not an item of that kind.</exception>
    public static SyncItem Parse(SyncItemKey key, string content, StepRegistry steps)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(steps);
        return key.Kind switch
        {
            SyncItemKind.Gesture => new GestureItem(SyncItemJson.ReadGesture(content)),
            SyncItemKind.Group => new GroupItem(SyncItemJson.ReadGroupHeader(content, steps)),
            SyncItemKind.Category => FromPair(SyncItemJson.ReadCategory(content)),
            SyncItemKind.Command => FromPair(SyncItemJson.ReadCommand(content, steps)),
            SyncItemKind.CommandVersion => FromVersion(SyncItemJson.ReadVersion(content, steps)),
            SyncItemKind.HoldRemap => FromPair(SyncItemJson.ReadHoldRemap(content)),
            _ => new IgnoredItem(SyncItemJson.ReadIgnored(content)),
        };
    }

    private static CategoryItem FromPair((GroupId Group, CommandCategory Category) pair) => new(pair.Group, pair.Category);

    private static CommandItem FromPair((GroupId Group, Command Command) pair) => new(pair.Group, pair.Command);

    private static HoldRemapItem FromPair((GroupId Group, HoldRemap HoldRemap) pair) => new(pair.Group, pair.HoldRemap);

    private static VersionItem FromVersion((CommandId Command, CommandVersion Version) pair) => new(pair.Command, null, pair.Version);

    public sealed class GestureItem : SyncItem
    {
        public GestureItem(Gesture gesture)
            : base(SyncItemKey.ForGesture(gesture.Id), gesture.Name, SyncItemJson.Gesture(gesture))
        {
            Gesture = gesture;
        }

        public Gesture Gesture { get; }
    }

    /// <summary>A group without its commands, categories and hold remaps, which are items of their own.</summary>
    public sealed class GroupItem : SyncItem
    {
        public GroupItem(AppGroup group)
            : base(SyncItemKey.ForGroup(group.Id), group.Name, SyncItemJson.GroupHeader(group))
        {
            Header = group with { Commands = [], Categories = [], HoldRemaps = [] };
        }

        public AppGroup Header { get; }
    }

    public sealed class CategoryItem : SyncItem
    {
        public CategoryItem(GroupId groupId, CommandCategory category)
            : base(SyncItemKey.ForCategory(groupId, category.Id), category.Name, SyncItemJson.Category(groupId, category))
        {
            GroupId = groupId;
            Category = category;
        }

        public GroupId GroupId { get; }

        public CommandCategory Category { get; }
    }

    /// <summary>A hold remap's header with its group (F9); the commands under it are <see cref="CommandItem"/>s naming it.</summary>
    public sealed class HoldRemapItem : SyncItem
    {
        public HoldRemapItem(GroupId groupId, HoldRemap holdRemap)
            : base(SyncItemKey.ForHoldRemap(groupId, holdRemap.Id), holdRemap.Name, SyncItemJson.HoldRemap(groupId, holdRemap))
        {
            GroupId = groupId;
            HoldRemap = holdRemap;
        }

        public GroupId GroupId { get; }

        public HoldRemap HoldRemap { get; }
    }

    /// <summary>A command with its group; its own version, when it has one, is a <see cref="VersionItem"/> of its own.</summary>
    public sealed class CommandItem : SyncItem
    {
        public CommandItem(GroupId groupId, Command command)
            : base(SyncItemKey.ForCommand(command.Id), command.Name, SyncItemJson.Command(groupId, command with { OwnVersion = null }))
        {
            GroupId = groupId;
            Command = command with { OwnVersion = null };
        }

        public GroupId GroupId { get; }

        public Command Command { get; }
    }

    /// <summary>A command's own steps for one platform (F8), keyed by the command; named after it when the command is known.</summary>
    public sealed class VersionItem : SyncItem
    {
        public VersionItem(CommandId commandId, string? commandName, CommandVersion version)
            : base(SyncItemKey.ForCommandVersion(commandId), $"{commandName ?? "A command"} ({PlatformName(version)} steps)", SyncItemJson.Version(commandId, version))
        {
            CommandId = commandId;
            Version = version;
        }

        public CommandId CommandId { get; }

        public CommandVersion Version { get; }

        private static string PlatformName(CommandVersion version) => version.Platform == Abstractions.HostPlatform.MacOS ? "macOS" : "Windows";
    }

    public sealed class IgnoredItem : SyncItem
    {
        public IgnoredItem(IgnoredApp app)
            : base(SyncItemKey.ForIgnored(app.Id), app.Name, SyncItemJson.Ignored(app))
        {
            App = app;
        }

        public IgnoredApp App { get; }
    }
}
