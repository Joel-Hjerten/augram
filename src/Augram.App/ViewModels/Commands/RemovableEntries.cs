using System.ComponentModel;
using Augram.App.Declarations;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The ✕ of a check list of Exclusions entries, the Not in and Also in dialogs' (Joel, 2026-10-12: the lists only grew): an entry
/// no other command names (<see cref="UsedElsewhere"/>, over Core's <see cref="MappingDocument.UsersOf"/>) and not ticked here
/// reads "unused" (<see cref="UnusedNote"/>, muted) with an ✕ that takes it out of the list (<see cref="Remove"/>). It keeps
/// <see cref="Removed"/>, the stored entries ✕ took away, which the host deletes with the ticks as one edit on Save (Cancel removes
/// nothing). The dialog keeps what it offers (<c>entries</c>, which <see cref="Remove"/> takes from), its ticks (a
/// <see cref="CheckedSet{TId}"/>) and, the Not in dialog, the entries its Add app… made (<c>added</c>: removing one of those
/// adds nothing rather than removing anything; the Also in dialog has none). The words that differ per list, which Exclusions
/// list the entry lives on, are <see cref="Tip"/>'s and <see cref="Help"/>'s parameter; the note is the same for both.
/// </summary>
internal sealed class RemovableEntries
{
    public const string UnusedNote = "unused";

    private readonly List<IgnoredApp> _entries;
    private readonly List<IgnoredApp> _added;
    private readonly CheckedSet<GroupId> _ticks;
    private readonly HashSet<GroupId> _usedElsewhere;
    private readonly string _tip;
    private readonly INotifyPropertyChanged _owner;
    private readonly string _ticksPropertyName;
    private readonly Action _removed;
    private readonly List<GroupId> _removedIds = [];

    /// <param name="entries">The entries the dialog offers; a removed one is taken out of it.</param>
    /// <param name="ticks">The dialog's ticks: a ticked entry is never removable.</param>
    /// <param name="usedElsewhere">The entries other commands name: never removable. None when null.</param>
    /// <param name="tip">The ✕'s tooltip (<see cref="Tip"/>).</param>
    /// <param name="owner">The dialog: each ✕ re-reads its removable state when it raises <paramref name="ticksPropertyName"/>.</param>
    /// <param name="ticksPropertyName">The dialog's property raised when a tick changes (its ticked ids).</param>
    /// <param name="removed">Called after an entry left <paramref name="entries"/> (the dialog raises its entries property, and the list rebuilds).</param>
    /// <param name="added">The entries the dialog's Add app… made, when it has one; a removed one of those is taken out of this list instead of joining <see cref="Removed"/>.</param>
    public RemovableEntries(List<IgnoredApp> entries, CheckedSet<GroupId> ticks, IEnumerable<GroupId>? usedElsewhere, string tip, INotifyPropertyChanged owner, string ticksPropertyName, Action removed, List<IgnoredApp>? added = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(ticks);
        ArgumentNullException.ThrowIfNull(tip);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(ticksPropertyName);
        ArgumentNullException.ThrowIfNull(removed);
        _entries = entries;
        _added = added ?? [];
        _ticks = ticks;
        _usedElsewhere = [.. usedElsewhere ?? []];
        _tip = tip;
        _owner = owner;
        _ticksPropertyName = ticksPropertyName;
        _removed = removed;
    }

    /// <summary>The stored entries ✕ took away, in the order removed; the host deletes them from their Exclusions list with the ticks on Save.</summary>
    public IReadOnlyList<GroupId> Removed => _removedIds;

    /// <summary>"Remove from Exclusions › Per command": the ✕'s tooltip for the entries of <paramref name="list"/> ("Per command", "Global").</summary>
    public static string Tip(string list) => $"Remove from Exclusions › {list}";

    /// <summary>The sentence a list's help adds after the row's own: what "unused" and the ✕ mean for the entries of <paramref name="list"/>.</summary>
    public static string Help(string list)
        => $" An app no other command names reads \"{UnusedNote}\": its ✕ deletes it from Exclusions › {list}, the only place it lives, when you Save.";

    /// <summary>Of <paramref name="entries"/>, the ones a command other than <paramref name="command"/> names: the dialog never offers those for removal.</summary>
    public static IEnumerable<GroupId> UsedElsewhere(MappingDocument mapping, IEnumerable<IgnoredApp> entries, CommandId command)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(entries);
        return entries.Where(app => mapping.UsersOf(app.Id).Any(pair => pair.Command.Id != command)).Select(app => app.Id);
    }

    /// <summary>True when no other command names the entry and it is not ticked here: it reads "unused" and may be removed.</summary>
    public bool IsRemovable(GroupId id) => !_usedElsewhere.Contains(id) && !_ticks.IsChecked(id) && _entries.Exists(app => app.Id == id);

    /// <summary>
    /// ✕: takes a removable entry (<see cref="IsRemovable"/>) out of the list; one Add app… made in the dialog is simply not added, a
    /// stored one joins <see cref="Removed"/>. Anything else is left alone. Returns whether it was removed.
    /// </summary>
    public bool Remove(GroupId id)
    {
        if (!IsRemovable(id))
        {
            return false;
        }

        var app = _entries.Find(entry => entry.Id == id)!;
        _entries.Remove(app);
        if (!_added.Remove(app))
        {
            _removedIds.Add(id);
        }

        _removed();
        return true;
    }

    /// <summary>The box for <paramref name="id"/> (a <see cref="CheckedSet{TId}.Item"/>) with its ✕: removable while <see cref="IsRemovable"/>, re-read when a tick changes.</summary>
    public CheckListItem WithRemove(CheckListItem item, GroupId id)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item with
        {
            Remove = new CheckListRemove(UnusedNote, _tip, () => Remove(id), new DelegateBinding<bool>(() => IsRemovable(id), owner: _owner, propertyName: _ticksPropertyName)),
        };
    }
}
