using Augram.App.Declarations;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The category form's edit state (Joel, 2026-10-08: "Use on" per Global category): the name and where the category's
/// commands are used ("Use on" Windows and macOS). <see cref="Declare"/> is the form as a <see cref="FormScreen"/>
/// (ADR-0002 §5c), shown in the Global tab's side panel while a category row is selected, as an app group's form is on the
/// Apps tab; <see cref="Apply"/> turns it back into the category, its id kept; <see cref="SyncFrom"/> re-reads a stored
/// category after an undo or a rename in the tree. Nothing is validated here: the store's rules answer when the panel applies.
/// </summary>
public sealed partial class CategoryEditViewModel : ObservableObject
{
    public CategoryEditViewModel()
    {
        UseOn.PropertyChanged += (_, e) => OnPropertyChanged(e);
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>Where the category's commands are used; its changes are raised as this view model's own. A refused change is put back with its <see cref="UseOnEdit.SyncFrom"/>, leaving the name as typed.</summary>
    public UseOnEdit UseOn { get; } = new();

    public static CategoryEditViewModel From(CommandCategory category)
    {
        var edit = new CategoryEditViewModel();
        edit.SyncFrom(category);
        return edit;
    }

    /// <summary>Takes the stored category's values; a property that already holds the value raises nothing.</summary>
    public void SyncFrom(CommandCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        Name = category.Name;
        UseOn.SyncFrom(category.UseOn);
    }

    /// <summary>The existing category with these settings.</summary>
    public CommandCategory Apply(CommandCategory existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing with { Name = Name, UseOn = UseOn.Value };
    }

    public FormScreen Declare() => new("Category",
    [
        new Section("Category",
        [
            new TextField("Name", new DelegateBinding<string>(() => Name, value => Name = value, this), "Shown as a section of the Global tab; unique among categories."),
            UseOn.Declare(
                "Applies to every command in the category: a platform left unticked never fires them and hides the category unless Show other platforms is on. Each command keeps its own Use on for when the category takes the platform back."),
        ]),
    ]);
}
