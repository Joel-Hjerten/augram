using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Sync;

/// <summary>
/// One mergeable unit of the synced data (README: items), a closed set like <c>Trigger</c>: a gesture, an
/// app group's header, a category with its group, a command with its group, an ignored app. Each carries its
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
            _ => new IgnoredItem(SyncItemJson.ReadIgnored(content)),
        };
    }

    private static CategoryItem FromPair((GroupId Group, CommandCategory Category) pair) => new(pair.Group, pair.Category);

    private static CommandItem FromPair((GroupId Group, Command Command) pair) => new(pair.Group, pair.Command);

    public sealed class GestureItem : SyncItem
    {
        public GestureItem(Gesture gesture)
            : base(SyncItemKey.ForGesture(gesture.Id), gesture.Name, SyncItemJson.Gesture(gesture))
        {
            Gesture = gesture;
        }

        public Gesture Gesture { get; }
    }

    /// <summary>A group without its commands and categories, which are items of their own.</summary>
    public sealed class GroupItem : SyncItem
    {
        public GroupItem(AppGroup group)
            : base(SyncItemKey.ForGroup(group.Id), group.Name, SyncItemJson.GroupHeader(group))
        {
            Header = group with { Commands = [], Categories = [] };
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

    public sealed class CommandItem : SyncItem
    {
        public CommandItem(GroupId groupId, Command command)
            : base(SyncItemKey.ForCommand(command.Id), command.Name, SyncItemJson.Command(groupId, command))
        {
            GroupId = groupId;
            Command = command;
        }

        public GroupId GroupId { get; }

        public Command Command { get; }
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
