using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.State;

namespace Augram.Core.Mapping;

/// <summary>
/// The single mutable owner of the mapping for the running app (ADR-0002 §5a), the same shape as
/// <c>GestureLibrary</c> and <c>SettingsStore</c>: every mutation builds the next document, validates
/// and sorts it through <see cref="MappingRules.ValidDocument"/>, records the previous snapshot for
/// <see cref="Undo"/>, replaces <see cref="Current"/> wholesale and raises <see cref="Changed"/>.
/// A mutation that breaks a rule throws before anything is recorded. Single-writer: the UI thread
/// mutates; the engine takes <see cref="Current"/> as an immutable snapshot when told the version
/// moved and resolves against it (<see cref="CommandResolver"/>) on its own thread.
/// </summary>
public sealed partial class MappingStore
{
    private readonly UndoStack<MappingDocument> _history = new();

    public MappingStore()
        : this(MappingDocument.Empty)
    {
    }

    /// <exception cref="MappingValidationException">The initial document breaks a rule; see <see cref="MappingRules.ValidDocument"/>.</exception>
    public MappingStore(MappingDocument initial)
    {
        Current = MappingRules.ValidDocument(initial);
    }

    /// <summary>An immutable, sorted snapshot; a new instance after every change.</summary>
    public MappingDocument Current { get; private set; }

    /// <summary>Increments on every change, undo and redo included.</summary>
    public int Version { get; private set; }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public event EventHandler? Changed;

    public AppGroup Global => Current.Global;

    public AppGroup? FindGroup(GroupId id) => Current.Groups.FirstOrDefault(group => group.Id == id);

    public (AppGroup Group, Command Command)? FindCommand(CommandId id)
    {
        foreach (var pair in Current.AllCommands())
        {
            if (pair.Command.Id == id)
            {
                return pair;
            }
        }

        return null;
    }

    public IgnoredApp? FindIgnored(GroupId id) => Current.Ignored.FirstOrDefault(app => app.Id == id);

    /// <summary>Every command bound to the gesture, across all groups in document order: the "Used by…" popup and the delete warning.</summary>
    public IReadOnlyList<(AppGroup Group, Command Command)> UsedBy(GestureId gestureId)
        => Current.AllCommands().Where(pair => pair.Command.UsesGesture(gestureId)).ToArray();

    /// <summary>Appends; returns the group as stored (trimmed, commands sorted).</summary>
    public AppGroup AddGroup(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Commit(Current with { Groups = [.. Current.Groups, group] });
        return FindGroup(group.Id)!;
    }

    /// <summary>Replaces the group with the same id, commands and categories included (adding, renaming or deleting a category is this call: one undo step).</summary>
    public AppGroup UpdateGroup(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var groups = Current.Groups.ToArray();
        groups[IndexOfGroup(group.Id)] = group;
        Commit(Current with { Groups = groups });
        return FindGroup(group.Id)!;
    }

    /// <summary>Removes the group and its commands; <see cref="Undo"/> brings them back.</summary>
    /// <exception cref="MappingValidationException">The Global group (F5a: it cannot be deleted).</exception>
    public AppGroup RemoveGroup(GroupId id)
    {
        if (id == GroupId.Global)
        {
            throw new MappingValidationException("The Global group cannot be removed.");
        }

        var removed = RequireGroup(id);
        Commit(Current with { Groups = Current.Groups.Where(group => group.Id != id).ToArray() });
        return removed;
    }

    public Command AddCommand(GroupId groupId, Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var group = RequireGroup(groupId);
        UpdateGroup(group with { Commands = [.. group.Commands, command] });
        return FindCommand(command.Id)!.Value.Command;
    }

    /// <summary>Replaces the command with the same id in that group.</summary>
    public Command UpdateCommand(GroupId groupId, Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var group = RequireGroup(groupId);
        var commands = group.Commands.ToArray();
        commands[IndexOfCommand(group, command.Id)] = command;
        UpdateGroup(group with { Commands = commands });
        return FindCommand(command.Id)!.Value.Command;
    }

    public Command RemoveCommand(CommandId id)
    {
        var (group, removed) = RequireCommand(id);
        UpdateGroup(group with { Commands = group.Commands.Where(command => command.Id != id).ToArray() });
        return removed;
    }

    /// <summary>
    /// Moves the command into another group (paste), keeping its id. Its category goes with it only when
    /// the target group has a category of the same name (case-insensitive), whose id it takes; otherwise
    /// it lands Uncategorized. Its hold remap goes with it only when the target group has one on the same hold key; otherwise
    /// it lands an ordinary command without its input (<see cref="HoldRemapRules.Moved"/>). One undo step.
    /// </summary>
    public Command MoveCommand(CommandId id, GroupId toGroupId)
    {
        var (from, command) = RequireCommand(id);
        var to = RequireGroup(toGroupId);
        if (from.Id == to.Id)
        {
            return command;
        }

        var moved = HoldRemapRules.Moved(command, from, to) with { CategoryId = CategoryRules.Carried(command.CategoryId, from, to) };
        var groups = Current.Groups.Select(group =>
            group.Id == from.Id ? group with { Commands = group.Commands.Where(other => other.Id != id).ToArray() }
            : group.Id == to.Id ? group with { Commands = [.. group.Commands, moved] }
            : group).ToArray();
        Commit(Current with { Groups = groups });
        return FindCommand(id)!.Value.Command;
    }

    public IgnoredApp AddIgnored(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Commit(Current with { Ignored = [.. Current.Ignored, app] });
        return FindIgnored(app.Id)!;
    }

    public IgnoredApp UpdateIgnored(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var ignored = Current.Ignored.ToArray();
        ignored[IndexOfIgnored(app.Id)] = app;
        Commit(Current with { Ignored = ignored });
        return FindIgnored(app.Id)!;
    }

    public IgnoredApp RemoveIgnored(GroupId id)
    {
        var removed = Current.Ignored[IndexOfIgnored(id)];
        Commit(Current with { Ignored = Current.Ignored.Where(app => app.Id != id).ToArray() });
        return removed;
    }

    /// <summary>Replaces the whole mapping (import). The document is validated first; one undo step.</summary>
    public MappingDocument ReplaceAll(MappingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Commit(document);
        return Current;
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        Current = _history.Undo(Current);
        Bump();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        Current = _history.Redo(Current);
        Bump();
        return true;
    }

    /// <summary>Forgets the undo history, e.g. after a load that should not be undoable.</summary>
    public void ClearHistory() => _history.Clear();

    private AppGroup RequireGroup(GroupId id) => Current.Groups[IndexOfGroup(id)];

    private (AppGroup Group, Command Command) RequireCommand(CommandId id)
        => FindCommand(id) ?? throw new KeyNotFoundException($"No command with id {id}.");

    private int IndexOfGroup(GroupId id)
    {
        int index = IndexOf(Current.Groups, group => group.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"No app group with id {id}.");
    }

    private static int IndexOfCommand(AppGroup group, CommandId id)
    {
        int index = IndexOf(group.Commands, command => command.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"No command with id {id} in '{group.Name}'.");
    }

    private int IndexOfIgnored(GroupId id)
    {
        int index = IndexOf(Current.Ignored, app => app.Id == id);
        return index >= 0 ? index : throw new KeyNotFoundException($"No ignored app with id {id}.");
    }

    private static int IndexOf<T>(IReadOnlyList<T> items, Func<T, bool> matches)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (matches(items[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private void Commit(MappingDocument next)
    {
        var valid = MappingRules.ValidDocument(next);
        _history.Record(Current);
        Current = valid;
        Bump();
    }

    private void Bump()
    {
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
