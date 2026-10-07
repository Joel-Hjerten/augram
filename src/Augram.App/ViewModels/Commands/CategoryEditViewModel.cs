using Augram.App.Declarations;
using Augram.Core.Abstractions;
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
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool UseOnWindows { get; set; } = true;

    [ObservableProperty]
    public partial bool UseOnMac { get; set; } = true;

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
        SyncUseOnFrom(category);
    }

    /// <summary>Takes only the stored "Use on", leaving the name as typed (a refused change puts the boxes back).</summary>
    public void SyncUseOnFrom(CommandCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);
        UseOnWindows = category.UseOn.Includes(HostPlatform.Windows);
        UseOnMac = category.UseOn.Includes(HostPlatform.MacOS);
    }

    /// <summary>The existing category with these settings.</summary>
    public CommandCategory Apply(CommandCategory existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing with { Name = Name, UseOn = PlatformSet.None.With(HostPlatform.Windows, UseOnWindows).With(HostPlatform.MacOS, UseOnMac) };
    }

    public FormScreen Declare() => new("Category",
    [
        new Section("Category",
        [
            new TextField("Name", new DelegateBinding<string>(() => Name, value => Name = value, this), "Shown as a section of the Global tab; unique among categories."),
            new TogglesField(
                "Use on",
                [
                    new ToggleOption("Windows", new DelegateBinding<bool>(() => UseOnWindows, value => UseOnWindows = value, this)),
                    new ToggleOption("macOS", new DelegateBinding<bool>(() => UseOnMac, value => UseOnMac = value, this)),
                ],
                "Applies to every command in the category: a platform left unticked never fires them and hides the category unless Show other platforms is on. Each command keeps its own Use on for when the category takes the platform back."),
        ]),
    ]);
}
