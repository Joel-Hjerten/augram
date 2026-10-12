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

    /// <summary>Increments on every change, undo and redo included: what tests count (the app listens to <see cref="Changed"/>).</summary>
    internal int Version { get; private set; }

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public event EventHandler? Changed;

    public AppGroup Global => Current.Global;

    public AppGroup? FindGroup(GroupId id) => Current.Groups.FirstOrDefault(group => group.Id == id);

    public (AppGroup Group, Command Command)? FindCommand(CommandId id) => Current.FindCommand(id);

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
    /// <exception cref="MappingValidationException">The change breaks a rule, renaming the Global group among them (<see cref="MappingRules.EnsureNotRenamingGlobal"/>).</exception>
    public AppGroup UpdateGroup(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var groups = Current.Groups.ToArray();
        var index = IndexOfGroup(group.Id);
        MappingRules.EnsureNotRenamingGlobal(groups[index], group);
        groups[index] = group;
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
        Commit(WithCommandReplaced(groupId, command));
        return FindCommand(command.Id)!.Value.Command;
    }

    /// <summary>
    /// Adds <paramref name="added"/> to the ignore list, removes the entries <paramref name="removed"/> names, and replaces the command
    /// with the same id in that group, as one change and one undo step (plan 0004: the "Not in" dialog's Save, where Add app… made new
    /// Exclusions › Per command entries the command's <see cref="Command.NotIn"/> names, and ✕ took away entries no command uses; Joel,
    /// 2026-10-12). Refused as a whole when either part breaks a rule, or when a command, this one as replaced included, still names
    /// a removed entry (the dialog offers ✕ only on an entry nothing else uses and not ticked).
    /// </summary>
    /// <exception cref="MappingValidationException">A rule is broken, or a removed entry is still named in a "Not in".</exception>
    public Command UpdateCommandChangingIgnored(GroupId groupId, Command command, IReadOnlyList<IgnoredApp> added, IReadOnlyCollection<GroupId> removed)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(added);
        ArgumentNullException.ThrowIfNull(removed);
        var replaced = WithCommandReplaced(groupId, command);
        if (Current.Ignored.FirstOrDefault(app => removed.Contains(app.Id) && replaced.NotInUsers(app.Id).Any()) is { } used)
        {
            throw new MappingValidationException($"'{used.Name}' is still used in a command's Not in, so it cannot be removed.");
        }

        Commit(replaced with { Ignored = [.. Current.Ignored.Where(app => !removed.Contains(app.Id)), .. added] });
        return FindCommand(command.Id)!.Value.Command;
    }

    /// <summary>
    /// Makes <paramref name="commands"/> exactly the commands whose "Also in" names the Exclusions › Global entry
    /// <paramref name="excluded"/> (plan 0005: its "Allowed for", edited from the entry's side; Joel, 2026-10-11), as one change and
    /// one undo step; returns how many commands changed, and commits nothing when none did. Refused as a whole when the result
    /// breaks a rule; a command that cannot work over an excluded app (<see cref="MappingRules.CanWorkOverExcluded"/>) is never
    /// given it.
    /// </summary>
    public int SetAllowedFor(GroupId excluded, IReadOnlyCollection<CommandId> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var changed = 0;
        Command Allowed(Command command)
        {
            var wanted = commands.Contains(command.Id) && MappingRules.CanWorkOverExcluded(command);
            if (wanted == command.AlsoIn.Contains(excluded))
            {
                return command;
            }

            changed++;
            return command with { AlsoIn = wanted ? [.. command.AlsoIn, excluded] : [.. command.AlsoIn.Where(id => id != excluded)] };
        }

        var groups = Current.Groups.Select(group => group with { Commands = [.. group.Commands.Select(Allowed)] }).ToArray();
        if (changed > 0)
        {
            Commit(Current with { Groups = groups });
        }

        return changed;
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
    /// it has none (into Global: it lands in Uncategorized). Its hold remap goes with it only when the target group has one on the same hold key; otherwise
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

    /// <summary>The current document with the command of the same id in that group replaced, not yet committed.</summary>
    private MappingDocument WithCommandReplaced(GroupId groupId, Command command) => Replaced(Current, groupId, command);

    /// <summary><paramref name="document"/> with the command of the same id in that group replaced, not validated.</summary>
    private static MappingDocument Replaced(MappingDocument document, GroupId groupId, Command command)
    {
        var index = IndexOf(document.Groups, group => group.Id == groupId);
        if (index < 0)
        {
            throw new KeyNotFoundException($"No app group with id {groupId}.");
        }

        var group = document.Groups[index];
        var commands = group.Commands.ToArray();
        commands[IndexOfCommand(group, command.Id)] = command;
        var groups = document.Groups.ToArray();
        groups[index] = group with { Commands = commands };
        return document with { Groups = groups };
    }

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
