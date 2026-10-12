using Augram.App.Components.CommandHeader;
using Augram.App.Declarations;
using Augram.App.Dialogs;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The Also in dialog's edit state (Joel, 2026-10-10, plan 0005 decision 7): the Exclusions › Global entries a command whose
/// trigger holds no stroke button still works over, as a check list (<see cref="CheckListField"/>, the Not in dialog's) of the
/// entries without the disable-while-focused mode, by name, the ones it names ticked. No Add app…: an app gets on Exclusions ›
/// Global on its own tab. An entry no other command names and not ticked here reads "unused" (muted) with an ✕ that takes it out
/// of the list (<see cref="Remove"/>; Joel, 2026-10-12, the Not in dialog's mechanism, <see cref="RemovableEntries"/>).
/// <see cref="Declare"/> is the form for the shared <c>FormDialog</c>; <see cref="AlsoIn"/> is what is ticked and
/// <see cref="Removed"/> the entries ✕ took away, which the host stores together as one edit when the dialog is confirmed (Cancel
/// removes nothing). What a list may hold is Core's (<see cref="MappingRules"/> keeps only such entries that exist, and none on a
/// trigger that holds the stroke button).
/// </summary>
public sealed class AlsoInEditViewModel : ObservableObject, IDialogForm
{
    public const string Title = "Also in";
    public const string ConfirmLabel = "Save";
    public const string ListLabel = "Apps";
    public const string ListName = "Global";
    public const string NoAppsText = "None on Exclusions › Global yet (an app there that disables Augram while focused never counts).";
    public static readonly string RemoveTip = RemovableEntries.Tip(ListName);
    public static readonly string ListHelp = CommandHeader.AlsoInHelp + RemovableEntries.Help(ListName);

    private readonly List<IgnoredApp> _entries;
    private readonly CheckedSet<GroupId> _checked;
    private readonly RemovableEntries _removable;

    /// <param name="commandName">The command's name, for the section title.</param>
    /// <param name="ignored">The whole exclusion list: its Global entries without the disable-while-focused mode are offered.</param>
    /// <param name="alsoIn">The entries the command names now, ticked.</param>
    /// <param name="usedElsewhere">The entries other commands name in their Also in: never offered for removal. None when null.</param>
    public AlsoInEditViewModel(string commandName, IEnumerable<IgnoredApp> ignored, IEnumerable<GroupId> alsoIn, IEnumerable<GroupId>? usedElsewhere = null)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        ArgumentNullException.ThrowIfNull(ignored);
        ArgumentNullException.ThrowIfNull(alsoIn);
        CommandName = commandName;
        _entries = [.. ignored.Where(Offered).OrderBy(app => app.Name, MappingRules.NameComparer).ThenBy(app => app.Id.Value)];
        _checked = new(alsoIn.Where(id => _entries.Exists(app => app.Id == id)), () => OnPropertyChanged(nameof(AlsoIn)));
        _removable = new(_entries, _checked, usedElsewhere, RemoveTip, this, nameof(AlsoIn), () => OnPropertyChanged(nameof(Entries)));
    }

    public string CommandName { get; }

    string IDialogForm.DialogTitle => Title;

    string IDialogForm.DialogConfirmLabel => ConfirmLabel;

    /// <summary>The entries offered, by name: Exclusions › Global, without the disable-while-focused mode.</summary>
    public IReadOnlyList<IgnoredApp> Entries => _entries;

    /// <summary>The ticked entries, in the stored order (by id).</summary>
    public IReadOnlyList<GroupId> AlsoIn => [.. _checked.OrderBy(id => id.Value)];

    /// <summary>The entries ✕ took away, in the order removed; Save deletes them from Exclusions › Global with <see cref="AlsoIn"/>.</summary>
    public IReadOnlyList<GroupId> Removed => _removable.Removed;

    /// <summary>The dialog for <paramref name="command"/> over <paramref name="mapping"/>'s exclusion list.</summary>
    public static AlsoInEditViewModel For(Command command, MappingDocument mapping)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(mapping);
        return new AlsoInEditViewModel(command.Name, mapping.Ignored, command.AlsoIn, RemovableEntries.UsedElsewhere(mapping, mapping.Ignored.Where(Offered), command.Id));
    }

    /// <summary>An entry an Also in may name: on Exclusions › Global, and not one that disables Augram while focused (that stops everything, hold remaps included).</summary>
    public static bool Offered(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return !app.IsPerCommand && !app.DisableEntirely;
    }

    /// <summary>True when no other command names the entry and it is not ticked here: it reads "unused" and may be removed.</summary>
    public bool IsRemovable(GroupId id) => _removable.IsRemovable(id);

    /// <summary>✕: takes a removable entry (<see cref="IsRemovable"/>) out of the list into <see cref="Removed"/>; anything else is left alone. Returns whether it was removed.</summary>
    public bool Remove(GroupId id) => _removable.Remove(id);

    public FormScreen Declare() => new(Title,
    [
        new Section($"Where '{CommandName}' still works",
        [
            new CheckListField(ListLabel, CheckItems(), ListHelp)
            {
                LiveItems = new DelegateBinding<IReadOnlyList<CheckListItem>>(CheckItems, owner: this, propertyName: nameof(Entries)),
                Visible = new DelegateBinding<bool>(() => _entries.Count > 0, owner: this, propertyName: nameof(Entries)),
            },
            new NoteField(ListLabel, NoAppsText, CommandHeader.AlsoInHelp)
            {
                Visible = new DelegateBinding<bool>(() => _entries.Count == 0, owner: this, propertyName: nameof(Entries)),
            },
        ]),
    ]);

    private IReadOnlyList<CheckListItem> CheckItems() => [.. _entries.Select(Item)];

    private CheckListItem Item(IgnoredApp app)
        => _removable.WithRemove(_checked.Item(app.Name, app.Id, this, nameof(AlsoIn), app.IsActive ? null : "inactive"), app.Id);
}
