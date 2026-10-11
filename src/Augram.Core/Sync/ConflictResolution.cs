using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Sync;

/// <summary>
/// The document side of answering conflicts (README: conflicts), pure, for the sync and the Augram import alike: what the
/// gestures and mapping become under the choices, repaired and validated like a merge result. Each <see cref="Answer"/> is one
/// item of the other side (null where they deleted it) with an effective choice (<see cref="SyncChoiceRules.Effective"/>),
/// in document order. Keep mine changes nothing. Take theirs puts theirs in place of ours, appends it when we have none, or
/// removes ours when theirs is a deletion. Keep both appends theirs under a new id; it applies to gestures and commands that
/// exist on both sides. The sync answers one conflict at a time (<see cref="Apply(SyncItemSet, SyncConflict, SyncChoice, StepRegistry)"/>);
/// the import answers every item of the file it adds or changes at once, which brings two extras the sync never meets: the
/// commands and own steps that arrive are bound to the copy of every gesture kept both, and a command's own steps follow their
/// command's answer (<see cref="Answer.FollowsCommand"/>: left out on Keep mine, in place of mine on Take theirs, on the copy
/// on Keep both). The result goes through <see cref="SyncDocumentBuilder.Build"/> with the arriving items as incoming.
/// </summary>
internal sealed class ConflictResolution
{
    private readonly SyncItemSet? _theirs;
    private readonly List<SyncItem?> _items;
    private readonly Dictionary<SyncItemKey, int> _positions;
    private readonly HashSet<SyncItemKey> _incoming = [];
    private readonly Dictionary<GestureId, GestureId> _gestureCopies = [];
    private readonly Dictionary<CommandId, (SyncChoice Choice, CommandId? Copy)> _commands = [];

    private ConflictResolution(SyncItemSet local, SyncItemSet? theirs)
    {
        _theirs = theirs;
        _items = [.. local];
        _positions = local.Select((item, index) => (item.Key, index)).ToDictionary(pair => pair.Key, pair => pair.index);
    }

    /// <summary>The resolved document for one sync conflict; <paramref name="choice"/> is an effective one (never Keep both where it does not apply).</summary>
    public static SyncMergeResult Apply(SyncItemSet local, SyncConflict conflict, SyncChoice choice, StepRegistry steps)
    {
        var theirs = conflict.RemoteContent is { } content ? SyncItem.Parse(conflict.Key, content, steps) : null;
        var built = Apply(local, [new Answer(conflict.Key, theirs, choice)]);
        return new SyncMergeResult(built.Gestures, built.Mapping, [], built.Repairs, SyncCounts.Between(local, built.Items))
        {
            Items = built.Items,
        };
    }

    /// <param name="local">The items here.</param>
    /// <param name="answers">The other side's items with their effective choices, in document order (a gesture before the commands using it, a command before its own steps).</param>
    /// <param name="theirs">The other side's items, to name the own steps that arrive for a copied or rebound command; null when none can arrive that way.</param>
    public static SyncDocumentBuilder.Built Apply(SyncItemSet local, IEnumerable<Answer> answers, SyncItemSet? theirs = null)
    {
        var resolution = new ConflictResolution(local, theirs);
        foreach (var answer in answers)
        {
            resolution.Take(answer);
        }

        return SyncDocumentBuilder.Build([.. resolution._items.OfType<SyncItem>()], resolution._incoming, local);
    }

    private void Take(Answer answer)
    {
        if (answer.FollowsCommand)
        {
            if (answer.Theirs is SyncItem.VersionItem version && _commands.TryGetValue(version.CommandId, out var command))
            {
                FollowCommand(version, command.Choice, command.Copy);
            }

            return;
        }

        CommandId? copy = null;
        if (answer.Choice == SyncChoice.TakeTheirs)
        {
            if (answer.Theirs is { } theirs)
            {
                Put(Rebound(theirs));
            }
            else if (_positions.TryGetValue(answer.Key, out int index))
            {
                _items[index] = null;
            }
        }
        else if (answer.Choice == SyncChoice.KeepBoth && answer.Theirs is { } theirs)
        {
            var added = Copy(theirs);
            Add(added);
            copy = (added as SyncItem.CommandItem)?.Command.Id;
        }

        if (answer.Theirs is SyncItem.CommandItem answered)
        {
            _commands[answered.Command.Id] = (answer.Choice, copy);
        }
    }

    /// <summary>The other side's own steps of an answered command: left out (Keep mine), in place of mine (Take theirs), or on the copy (Keep both).</summary>
    private void FollowCommand(SyncItem.VersionItem version, SyncChoice choice, CommandId? copy)
    {
        if (choice == SyncChoice.KeepBoth && copy is { } copyId)
        {
            Add(new SyncItem.VersionItem(copyId, CommandName(version.CommandId), Rebound(version.Version)));
        }
        else if (choice == SyncChoice.TakeTheirs)
        {
            Put(Rebound(version));
        }
    }

    /// <summary>Keep both: theirs under a new id; a gesture copy is remembered so the arriving commands bind to it.</summary>
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

    /// <summary>An arriving command or own steps bound to the copies of the gestures kept both; anything else as it is.</summary>
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

    private string? CommandName(CommandId id) => (_theirs?.Find(SyncItemKey.ForCommand(id)) as SyncItem.CommandItem)?.Command.Name;

    /// <summary>In place of the item here with that key, or appended when there is none.</summary>
    private void Put(SyncItem item)
    {
        if (_positions.TryGetValue(item.Key, out int index))
        {
            _items[index] = item;
            _incoming.Add(item.Key);
        }
        else
        {
            Add(item);
        }
    }

    private void Add(SyncItem item)
    {
        _items.Add(item);
        _incoming.Add(item.Key);
    }

    /// <summary>
    /// One item of the other side and the effective choice for it. <see cref="Theirs"/> is null where the other side deleted
    /// the item. <see cref="FollowsCommand"/> marks a command's own steps that go with their command's answer (the import's
    /// own steps of a differing command); its <see cref="Choice"/> is not read.
    /// </summary>
    internal sealed record Answer(SyncItemKey Key, SyncItem? Theirs, SyncChoice Choice)
    {
        public bool FollowsCommand { get; init; }
    }
}
