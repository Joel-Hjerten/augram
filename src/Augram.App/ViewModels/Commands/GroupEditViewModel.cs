using System.ComponentModel;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The app group form's edit state (F5 app identification, F5a "edit app definition", F8 per platform): name, active,
/// where the group is used ("Use on" Windows and macOS), suppress globals, and the app identification
/// (<see cref="Identification"/>, the form the Exclusions tab shares: executable names per platform with the known-app guess for
/// the platforms the group is used on, and out of the way path, title, classes and the full-screen rule). Its property
/// changes are raised as this view model's own, so a host listens in one place. <see cref="Declare"/> is the form as a
/// <see cref="FormScreen"/> (ADR-0002 §5c), shown in a <c>FormDialog</c> for a new group and in the Apps tab's side panel
/// for the selected one; <see cref="ToGroup"/> and <see cref="Apply"/> turn it back into an <see cref="AppGroup"/>;
/// <see cref="SyncFrom"/> re-reads a stored group after an undo or a rename in the tree. Nothing is validated here: the
/// store's rules answer when the dialog confirms or the panel applies.
/// </summary>
public sealed partial class GroupEditViewModel : ObservableObject
{
    public GroupEditViewModel()
    {
        Identification = new AppMatcherEditViewModel(
            () => UseOn,
            "type its name, or stop using the group there",
            "None needed: every platform the group is used on has its own names.");
        Identification.PropertyChanged += OnIdentificationChanged;
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool UseOnWindows { get; set; } = true;

    [ObservableProperty]
    public partial bool UseOnMac { get; set; } = true;

    [ObservableProperty]
    public partial bool SuppressGlobals { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    /// <summary>The app identification; its changes are raised as this view model's own (<see cref="AppMatcherEditViewModel.GuessText"/> among them).</summary>
    public AppMatcherEditViewModel Identification { get; }

    private PlatformSet UseOn => PlatformSet.None.With(HostPlatform.Windows, UseOnWindows).With(HostPlatform.MacOS, UseOnMac);

    public static GroupEditViewModel From(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var edit = new GroupEditViewModel();
        edit.SyncFrom(group);
        return edit;
    }

    /// <summary>Takes the stored group's values; a property that already holds the value raises nothing.</summary>
    public void SyncFrom(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        Name = group.Name;
        UseOnWindows = group.UseOn.Includes(HostPlatform.Windows);
        UseOnMac = group.UseOn.Includes(HostPlatform.MacOS);
        SuppressGlobals = group.SuppressGlobals;
        IsActive = group.IsActive;
        Identification.SyncFrom(group.Matcher ?? AppMatcher.Empty);
    }

    /// <summary>A new group with these settings and no commands.</summary>
    public AppGroup ToGroup(GroupId id) => new(id, Name, IsActive, SuppressGlobals, Identification.ToMatcher(), []) { UseOn = UseOn };

    /// <summary>The existing group with these settings, its commands untouched.</summary>
    public AppGroup Apply(AppGroup existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing with { Name = Name, IsActive = IsActive, SuppressGlobals = SuppressGlobals, UseOn = UseOn, Matcher = Identification.ToMatcher() };
    }

    public FormScreen Declare() => new("App group",
    [
        new Section("App group",
        [
            new TextField("Name", new DelegateBinding<string>(() => Name, value => Name = value, this), "Shown in the command tree; unique among groups."),
            new ToggleField("Active", new DelegateBinding<bool>(() => IsActive, value => IsActive = value, this), "An inactive group and its commands never fire."),
            new TogglesField(
                "Use on",
                [
                    new ToggleOption("Windows", new DelegateBinding<bool>(() => UseOnWindows, value => UseOnWindows = value, this)),
                    new ToggleOption("macOS", new DelegateBinding<bool>(() => UseOnMac, value => UseOnMac = value, this)),
                ],
                "A platform left unticked never fires the group and hides it from its list unless Show other platforms is on."),
            new ToggleField("Suppress global commands", new DelegateBinding<bool>(() => SuppressGlobals, value => SuppressGlobals = value, this), "SP.net's \"No Global Actions\": over this app only its own commands fire."),
        ]),
        .. Identification.Sections(),
    ]);

    partial void OnUseOnWindowsChanged(bool value) => Identification.UsedOnChanged();

    partial void OnUseOnMacChanged(bool value) => Identification.UsedOnChanged();

    private void OnIdentificationChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(e);
}
