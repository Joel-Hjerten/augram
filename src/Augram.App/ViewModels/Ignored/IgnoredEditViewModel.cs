using System.ComponentModel;
using Augram.App.Declarations;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The excluded app form's edit state (F5 ignore list; plan 0001 M2 step 6 "same form + disable-on-focus"): name, active, the
/// mode (<see cref="IgnoredModes"/>: gestures off over the app, or also Augram paused while it has focus), what the mode means
/// for hold remaps (plan 0005 decision 7: <see cref="IgnoredModes.HoldRemaps"/>) and the app identification app groups use
/// (<see cref="Identification"/>, whose changes are raised as this view model's own). An excluded app has no "Use on": it applies
/// wherever its names match, so the guess is shown for both platforms. An Exclusions › Per command entry (<see cref="Scope"/>,
/// plan 0004) has no mode, since it stops only the commands naming it, and shows them instead (<see cref="UsedBy"/>, which its
/// host keeps current; not an edit); a Global entry in the plain mode shows the commands still working over it
/// (<see cref="AllowedFor"/>, plan 0005, the same way). <see cref="Declare"/> is the form (a <c>FormDialog</c> for a new one,
/// the Exclusions tab's side panel for the selected one); <see cref="ToIgnored"/> and <see cref="Apply"/> turn it
/// back into an <see cref="IgnoredApp"/>; <see cref="SyncFrom"/> re-reads a stored one. Nothing is validated here.
/// </summary>
public sealed partial class IgnoredEditViewModel : ObservableObject
{
    public const string UsedByLabel = "Used by";
    public const string UsedByNone = "none";
    public const string UsedByHelp = "The commands that name this app in their Not in: over it they do nothing and hold no button back. "
        + "Click one to open it on the Commands tab; its Not in row's Change… ticks or unticks the app.";

    public const string AllowedForLabel = "Allowed for";
    public const string AllowedForNone = "none";
    public const string AllowedForHelp = "The commands that name this app in their Also in: over it they still work, holding only their own buttons back; the stroke button stays the app's. "
        + "Click one to open it on the Commands tab; its Also in row's Change… ticks or unticks the app.";

    /// <summary>The Global form's title and section: the UI word is "Exclusions" (plan 0005), the code's "Ignored".</summary>
    public const string GlobalTitle = "Excluded app";

    /// <param name="scope">The list the entry is on: Exclusions › Global (the default) or Exclusions › Per command.</param>
    public IgnoredEditViewModel(IgnoreScope scope = IgnoreScope.Global)
    {
        Scope = scope;
        Identification = new AppMatcherEditViewModel(
            () => PlatformSet.All,
            "type its name to exclude it there too",
            "None needed: both platforms have their own names.");
        Identification.PropertyChanged += OnIdentificationChanged;
    }

    /// <summary>Which list the entry is on; a Per command entry has no mode and shows <see cref="UsedBy"/>.</summary>
    public IgnoreScope Scope { get; }

    public bool IsPerCommand => Scope == IgnoreScope.PerCommand;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    /// <summary>The mode: false is "gestures off over this app", true is "disable while focused" (<see cref="IgnoredApp.DisableEntirely"/>).</summary>
    [ObservableProperty]
    public partial bool DisableWhileFocused { get; set; }

    /// <summary>A Per command entry's "Used by": the commands naming it, as links; set by the host, never an edit.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LinkItem> UsedBy { get; set; } = [];

    /// <summary>A Global entry's "Allowed for" (plan 0005): the commands whose Also in names it, as links; set by the host, never an edit.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<LinkItem> AllowedFor { get; set; } = [];

    /// <summary>The app identification, shared with app groups; its changes are raised as this view model's own.</summary>
    public AppMatcherEditViewModel Identification { get; }

    public static IgnoredEditViewModel From(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var edit = new IgnoredEditViewModel(app.Scope);
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

    /// <summary>A new ignored app with these settings, on this form's list.</summary>
    public IgnoredApp ToIgnored(GroupId id)
        => new(id, Name, IsActive, Identification.ToMatcher(), DisableWhileFocused && !IsPerCommand) { Scope = Scope };

    /// <summary>The existing ignored app with these settings, its id and its list kept.</summary>
    public IgnoredApp Apply(IgnoredApp existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing with
        {
            Name = Name,
            IsActive = IsActive,
            Matcher = Identification.ToMatcher(),
            DisableEntirely = existing.IsPerCommand ? existing.DisableEntirely : DisableWhileFocused,
        };
    }

    public FormScreen Declare() => IsPerCommand
        ? new("Per command app",
        [
            new Section("Per command app",
            [
                new TextField("Name", new DelegateBinding<string>(() => Name, value => Name = value, this), "Shown in the list and in the commands' Not in; unique among excluded apps."),
                new ToggleField("Active", new DelegateBinding<bool>(() => IsActive, value => IsActive = value, this), "An inactive entry stops no command: the commands naming it work over the app as usual."),
                new LinksField(UsedByLabel, new DelegateBinding<IReadOnlyList<LinkItem>>(() => UsedBy, owner: this, propertyName: nameof(UsedBy)), UsedByNone, UsedByHelp),
            ]),
            .. Identification.Sections(),
        ])
        : new(GlobalTitle,
        [
            new Section(GlobalTitle,
            [
                new TextField("Name", new DelegateBinding<string>(() => Name, value => Name = value, this), "Shown in the list."),
                new ToggleField("Active", new DelegateBinding<bool>(() => IsActive, value => IsActive = value, this), "An inactive entry excludes nothing: Augram works over the app as usual."),
                new ButtonRadioField<bool>(
                    "Mode",
                    IgnoredModes.Choices,
                    new DelegateBinding<bool>(() => DisableWhileFocused, value => DisableWhileFocused = value, this),
                    IgnoredModes.Help),

                // Plan 0005 decision 7: what the mode means for hold remaps, then the commands still working over it (none can in
                // the disable-while-focused mode).
                new NoteField(IgnoredModes.HoldRemapsLabel, new DelegateBinding<string>(() => IgnoredModes.HoldRemaps(DisableWhileFocused), owner: this, propertyName: nameof(DisableWhileFocused))),
                new LinksField(AllowedForLabel, new DelegateBinding<IReadOnlyList<LinkItem>>(() => AllowedFor, owner: this, propertyName: nameof(AllowedFor)), AllowedForNone, AllowedForHelp)
                {
                    Visible = new DelegateBinding<bool>(() => !DisableWhileFocused, owner: this, propertyName: nameof(DisableWhileFocused)),
                },
            ]),
            .. Identification.Sections(),
        ]);

    private void OnIdentificationChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(e);
}
