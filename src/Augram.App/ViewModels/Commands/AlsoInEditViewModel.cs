using Augram.App.Components.CommandTree;
using Augram.App.Declarations;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The Also in dialog's edit state (Joel, 2026-10-10, plan 0005 decision 7): the Exclusions › Global entries a command whose
/// trigger holds no stroke button still works over, as a check list (<see cref="CheckListField"/>, the Not in dialog's) of the
/// entries without the disable-while-focused mode, by name, the ones it names ticked. No Add app…: an app gets on Exclusions ›
/// Global on its own tab. <see cref="Declare"/> is the form for the shared <c>FormDialog</c>; <see cref="AlsoIn"/> is what is
/// ticked, which the host stores as one edit when the dialog is confirmed. What a list may hold is Core's
/// (<see cref="MappingRules"/> keeps only such entries that exist, and none on a trigger that holds the stroke button).
/// </summary>
public sealed class AlsoInEditViewModel : ObservableObject
{
    public const string Title = "Also in";
    public const string ConfirmLabel = "Save";
    public const string ListLabel = "Apps";
    public const string NoAppsText = "None on Exclusions › Global yet (an app there that disables Augram while focused never counts).";

    private readonly List<IgnoredApp> _entries;
    private readonly HashSet<GroupId> _checked;

    /// <param name="commandName">The command's name, for the section title.</param>
    /// <param name="ignored">The whole exclusion list: its Global entries without the disable-while-focused mode are offered.</param>
    /// <param name="alsoIn">The entries the command names now, ticked.</param>
    public AlsoInEditViewModel(string commandName, IEnumerable<IgnoredApp> ignored, IEnumerable<GroupId> alsoIn)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        ArgumentNullException.ThrowIfNull(ignored);
        ArgumentNullException.ThrowIfNull(alsoIn);
        CommandName = commandName;
        _entries = [.. ignored.Where(Offered).OrderBy(app => app.Name, MappingRules.NameComparer).ThenBy(app => app.Id.Value)];
        _checked = [.. alsoIn.Where(id => _entries.Exists(app => app.Id == id))];
    }

    public string CommandName { get; }

    /// <summary>The entries offered, by name: Exclusions › Global, without the disable-while-focused mode.</summary>
    public IReadOnlyList<IgnoredApp> Entries => _entries;

    /// <summary>The ticked entries, in the stored order (by id).</summary>
    public IReadOnlyList<GroupId> AlsoIn => [.. _checked.OrderBy(id => id.Value)];

    /// <summary>The dialog for <paramref name="command"/> over <paramref name="mapping"/>'s exclusion list.</summary>
    public static AlsoInEditViewModel For(Command command, MappingDocument mapping)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(mapping);
        return new AlsoInEditViewModel(command.Name, mapping.Ignored, command.AlsoIn);
    }

    /// <summary>An entry an Also in may name: on Exclusions › Global, and not one that disables Augram while focused (that stops everything, hold remaps included).</summary>
    public static bool Offered(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return !app.IsPerCommand && !app.DisableEntirely;
    }

    public bool IsChecked(GroupId id) => _checked.Contains(id);

    public void SetChecked(GroupId id, bool value)
    {
        if (value ? _checked.Add(id) : _checked.Remove(id))
        {
            OnPropertyChanged(nameof(AlsoIn));
        }
    }

    public FormScreen Declare() => new(Title,
    [
        new Section($"Where '{CommandName}' still works",
        [
            _entries.Count > 0
                ? new CheckListField(ListLabel, [.. _entries.Select(Item)], CommandHeader.AlsoInHelp)
                : new NoteField(ListLabel, NoAppsText, CommandHeader.AlsoInHelp),
        ]),
    ]);

    private CheckListItem Item(IgnoredApp app)
        => new(app.Name, new DelegateBinding<bool>(() => IsChecked(app.Id), value => SetChecked(app.Id, value), this, propertyName: nameof(AlsoIn)), app.IsActive ? null : "inactive");
}
