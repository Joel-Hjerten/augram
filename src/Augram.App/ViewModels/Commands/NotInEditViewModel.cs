using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.Declarations;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The Not in dialog's edit state (Joel, 2026-10-10, plan 0004): a Global command's "not used in these app groups" as a check
/// list (<see cref="CheckListField"/>, the export dialog's), the app groups by name, the ones it is not used in ticked.
/// <see cref="Declare"/> is the form for the shared <c>FormDialog</c>; <see cref="NotIn"/> is what is ticked, which the host
/// stores as one edit when the dialog is confirmed. What a list may hold is Core's (<see cref="MappingRules"/> keeps only app
/// groups that exist); this only collects the ticks, for the groups there were when the dialog opened.
/// </summary>
public sealed class NotInEditViewModel : ObservableObject
{
    public const string Title = "Not in";
    public const string ConfirmLabel = "Save";
    public const string ListLabel = "App groups";
    public const string NoGroupsText = "No app groups yet: add one on the Apps tab.";

    private readonly HashSet<GroupId> _checked;

    /// <param name="commandName">The command's name, for the section title.</param>
    /// <param name="groups">Every group of the mapping; Global is left out and the rest are listed by name.</param>
    /// <param name="notIn">The groups the command is not used in now, ticked.</param>
    public NotInEditViewModel(string commandName, IEnumerable<AppGroup> groups, IEnumerable<GroupId> notIn)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(notIn);
        CommandName = commandName;
        Groups = [.. groups.Where(group => !group.IsGlobal).OrderBy(group => group.Name, MappingRules.NameComparer)];
        _checked = [.. notIn.Where(id => Groups.Any(group => group.Id == id))];
    }

    public string CommandName { get; }

    /// <summary>The app groups offered, by name.</summary>
    public IReadOnlyList<AppGroup> Groups { get; }

    /// <summary>The ticked groups, in the stored order (by id).</summary>
    public IReadOnlyList<GroupId> NotIn => [.. _checked.OrderBy(id => id.Value)];

    /// <summary>The dialog for <paramref name="command"/> over <paramref name="mapping"/>'s app groups.</summary>
    public static NotInEditViewModel For(Command command, MappingDocument mapping)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(mapping);
        return new NotInEditViewModel(command.Name, mapping.Groups, command.NotIn);
    }

    public bool IsChecked(GroupId id) => _checked.Contains(id);

    public void SetChecked(GroupId id, bool value)
    {
        if (value ? _checked.Add(id) : _checked.Remove(id))
        {
            OnPropertyChanged(nameof(NotIn));
        }
    }

    public FormScreen Declare() => new(Title,
    [
        new Section($"Where '{CommandName}' is not used",
        [
            Groups.Count == 0
                ? new NoteField(ListLabel, NoGroupsText)
                : new CheckListField(ListLabel, [.. Groups.Select(Item)], CommandHeader.NotInHelp),
        ]),
    ]);

    /// <summary>"inactive", "Windows only", both, or no detail.</summary>
    private static string? Detail(AppGroup group)
    {
        var parts = new List<string>(2);
        if (!group.IsActive)
        {
            parts.Add("inactive");
        }

        if (group.UseOn != PlatformSet.All)
        {
            parts.Add(StepPlatformMarker.Only(group.UseOn));
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private CheckListItem Item(AppGroup group)
        => new(group.Name, new DelegateBinding<bool>(() => IsChecked(group.Id), value => SetChecked(group.Id, value), this, propertyName: nameof(NotIn)), Detail(group));
}
