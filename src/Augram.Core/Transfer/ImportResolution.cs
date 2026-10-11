using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Sync;

namespace Augram.Core.Transfer;

/// <summary>
/// An import's choices applied to its items (plan 0003, decisions 6–8), pure. The local items stay in their order; a new
/// file item is appended, a Same one skipped, a differing one goes by its effective choice
/// (<see cref="ConflictResolution.Effective"/>, the sync's: Keep both only for gestures and commands): Keep mine skips it,
/// Take theirs puts the file's in place of mine, Keep both appends the file's under a new id. A command's own steps follow
/// a differing command's choice (the file's own steps go onto the copy on Keep both). The file's commands that arrive (added,
/// taken or copied) are bound to the copy of every gesture kept both; yours keep yours. The result goes through
/// <see cref="SyncDocumentBuilder.Build"/> with the arriving items as incoming, so the sync's repairs (a name clash renamed
/// " (2)", a taken trigger unbound, dangling references, the hold remap rules) and validation apply as to any merge.
/// </summary>
internal sealed class ImportResolution
{
    private readonly SyncItemSet _local;
    private readonly SyncItemSet _file;
    private readonly List<SyncItem> _items;
    private readonly Dictionary<SyncItemKey, int> _positions;
    private readonly HashSet<SyncItemKey> _incoming = [];
    private readonly List<string> _notes = [];
    private readonly Dictionary<GestureId, GestureId> _gestureCopies = [];
    private readonly Dictionary<CommandId, (SyncChoice Choice, CommandId? Copy)> _commands = [];

    private ImportResolution(SyncItemSet local, SyncItemSet file)
    {
        _local = local;
        _file = file;
        _items = [.. local];
        _positions = _items.Select((item, index) => (item.Key, index)).ToDictionary(pair => pair.Key, pair => pair.index);
    }

    /// <param name="local">The items here.</param>
    /// <param name="file">The file's items, lined up by <see cref="ImportMatcher"/>.</param>
    /// <param name="conflicts">The plan's conflicts by key: every differing item that is not own steps following its command.</param>
    /// <param name="choices">The user's choices.</param>
    public static (SyncDocumentBuilder.Built Built, IReadOnlyList<string> Notes) Resolve(
        SyncItemSet local,
        SyncItemSet file,
        IReadOnlyDictionary<SyncItemKey, SyncConflict> conflicts,
        ImportChoices choices)
    {
        var resolution = new ImportResolution(local, file);
        foreach (var item in file)
        {
            resolution.Take(item, conflicts, choices);
        }

        return (SyncDocumentBuilder.Build(resolution._items, resolution._incoming, local), resolution._notes);
    }

    /// <summary>
    /// Decision 2: the file's preferences over this machine's options. Capture, trail, recognition and no-match whole, and the
    /// stroke button and ignore keys of General; this machine keeps its sync section and the rest of General (start at login,
    /// enabled, the menu-bar icon), which are its state, not preferences, and its appearance (plan 0006), a preference of its own.
    /// </summary>
    public static Settings WithOptionsFrom(Settings current, Settings file) => current with
    {
        General = current.General with { StrokeButton = file.General.StrokeButton, IgnoreKey = file.General.IgnoreKey },
        Capture = file.Capture,
        Trail = file.Trail,
        Recognition = file.Recognition,
        NoMatch = file.NoMatch,
    };

    private void Take(SyncItem item, IReadOnlyDictionary<SyncItemKey, SyncConflict> conflicts, ImportChoices choices)
    {
        if (item is SyncItem.VersionItem version && _commands.TryGetValue(version.CommandId, out var command))
        {
            FollowCommand(version, command.Choice, command.Copy);
            return;
        }

        var mine = _local.Find(item.Key);
        if (mine is null)
        {
            Add(Rebound(item));
            return;
        }

        if (string.Equals(mine.Content, item.Content, StringComparison.Ordinal))
        {
            return;
        }

        var (choice, note) = ConflictResolution.Effective(conflicts[item.Key], choices.For(item.Key));
        if (note is not null)
        {
            _notes.Add(note);
        }

        CommandId? copy = null;
        if (choice == SyncChoice.TakeTheirs)
        {
            Replace(Rebound(item));
        }
        else if (choice == SyncChoice.KeepBoth)
        {
            var added = Copy(item);
            Add(added);
            copy = (added as SyncItem.CommandItem)?.Command.Id;
        }

        if (item is SyncItem.CommandItem differing)
        {
            _commands[differing.Command.Id] = (choice, copy);
        }
    }

    /// <summary>The file's own steps of a differing command: left out (Keep mine), in place of mine (Take theirs), or on the copy (Keep both).</summary>
    private void FollowCommand(SyncItem.VersionItem version, SyncChoice choice, CommandId? copy)
    {
        if (choice == SyncChoice.KeepBoth && copy is { } copyId)
        {
            Add(new SyncItem.VersionItem(copyId, CommandName(version.CommandId), Rebound(version.Version)));
        }
        else if (choice == SyncChoice.TakeTheirs)
        {
            var taken = Rebound(version);
            if (_positions.ContainsKey(taken.Key))
            {
                Replace(taken);
            }
            else
            {
                Add(taken);
            }
        }
    }

    /// <summary>Keep both: the file's gesture or command under a new id; a gesture copy is remembered so the file's commands bind to it.</summary>
    private SyncItem Copy(SyncItem item)
    {
        switch (item)
        {
            case SyncItem.GestureItem gesture:
                var copy = gesture.Gesture with { Id = GestureId.New() };
                _gestureCopies[gesture.Gesture.Id] = copy.Id;
                return new SyncItem.GestureItem(copy);
            case SyncItem.CommandItem command:
                return new SyncItem.CommandItem(command.GroupId, command.Command.WithGesturesReplaced(_gestureCopies) with { Id = CommandId.New() });
            default:
                throw new InvalidOperationException($"Keep both does not apply to a {item.Kind}.");
        }
    }

    /// <summary>A file command or own steps bound to the copies of the gestures kept both; anything else as it is.</summary>
    private SyncItem Rebound(SyncItem item) => _gestureCopies.Count == 0 ? item : item switch
    {
        SyncItem.CommandItem command => new SyncItem.CommandItem(command.GroupId, command.Command.WithGesturesReplaced(_gestureCopies)),
        SyncItem.VersionItem version => new SyncItem.VersionItem(version.CommandId, CommandName(version.CommandId), Rebound(version.Version)),
        _ => item,
    };

    private CommandVersion Rebound(CommandVersion version)
        => version.Trigger is Trigger.GestureTrigger gesture && _gestureCopies.TryGetValue(gesture.GestureId, out var copy)
            ? version with { Trigger = gesture with { GestureId = copy } }
            : version;

    private string? CommandName(CommandId id) => (_file.Find(SyncItemKey.ForCommand(id)) as SyncItem.CommandItem)?.Command.Name;

    private void Add(SyncItem item)
    {
        _items.Add(item);
        _incoming.Add(item.Key);
    }

    private void Replace(SyncItem item)
    {
        _items[_positions[item.Key]] = item;
        _incoming.Add(item.Key);
    }
}
