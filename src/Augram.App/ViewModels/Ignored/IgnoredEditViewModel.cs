using System.ComponentModel;
using Augram.App.Declarations;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The ignored app form's edit state (F5 ignore list; plan 0001 M2 step 6 "same form + disable-on-focus"): name, active, the
/// mode (<see cref="IgnoredModes"/>: gestures off over the app, or also Augram paused while it has focus) and the app
/// identification app groups use (<see cref="Identification"/>, whose changes are raised as this view model's own). An ignored
/// app has no "Use on": it applies wherever its names match, so the guess is shown for both platforms. <see cref="Declare"/>
/// is the form (a <c>FormDialog</c> for a new one, the Ignored tab's side panel for the selected one); <see cref="ToIgnored"/>
/// and <see cref="Apply"/> turn it back into an <see cref="IgnoredApp"/>; <see cref="SyncFrom"/> re-reads a stored one.
/// Nothing is validated here.
/// </summary>
public sealed partial class IgnoredEditViewModel : ObservableObject
{
    public IgnoredEditViewModel()
    {
        Identification = new AppMatcherEditViewModel(
            () => PlatformSet.All,
            "type its name to ignore it there too",
            "None needed: both platforms have their own names.");
        Identification.PropertyChanged += OnIdentificationChanged;
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    /// <summary>The mode: false is "gestures off over this app", true is "disable while focused" (<see cref="IgnoredApp.DisableEntirely"/>).</summary>
    [ObservableProperty]
    public partial bool DisableWhileFocused { get; set; }

    /// <summary>The app identification, shared with app groups; its changes are raised as this view model's own.</summary>
    public AppMatcherEditViewModel Identification { get; }

    public static IgnoredEditViewModel From(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var edit = new IgnoredEditViewModel();
        edit.SyncFrom(app);
        return edit;
    }

    /// <summary>Takes the stored app's values; a property that already holds the value raises nothing.</summary>
    public void SyncFrom(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Name = app.Name;
        IsActive = app.IsActive;
        DisableWhileFocused = app.DisableEntirely;
        Identification.SyncFrom(app.Matcher);
    }

    /// <summary>A new ignored app with these settings.</summary>
    public IgnoredApp ToIgnored(GroupId id) => new(id, Name, IsActive, Identification.ToMatcher(), DisableWhileFocused);

    /// <summary>The existing ignored app with these settings, its id kept.</summary>
    public IgnoredApp Apply(IgnoredApp existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing with { Name = Name, IsActive = IsActive, Matcher = Identification.ToMatcher(), DisableEntirely = DisableWhileFocused };
    }

    public FormScreen Declare() => new("Ignored app",
    [
        new Section("Ignored app",
        [
            new TextField("Name", new DelegateBinding<string>(() => Name, value => Name = value, this), "Shown in the list."),
            new ToggleField("Active", new DelegateBinding<bool>(() => IsActive, value => IsActive = value, this), "An inactive entry is not ignored: Augram works over the app as usual."),
            new ButtonRadioField<bool>(
                "Mode",
                IgnoredModes.Choices,
                new DelegateBinding<bool>(() => DisableWhileFocused, value => DisableWhileFocused = value, this),
                IgnoredModes.Help),
        ]),
        .. Identification.Sections(),
    ]);

    private void OnIdentificationChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(e);
}
