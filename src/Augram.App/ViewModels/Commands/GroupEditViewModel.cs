using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The app group form's edit state (F5 app identification, F5a "edit app definition", F8 per platform): name, active,
/// where the group is used ("Use on" Windows and macOS), suppress globals, the Windows and the macOS executable names as
/// comma-separated text with the known-app guess for a platform left empty (<see cref="GuessText"/>), and, out of the
/// way, the window title with its regex toggle. <see cref="Declare"/> is the form as a <see cref="FormScreen"/>
/// (ADR-0002 §5c), shown in a <c>FormDialog</c> for a new group and in the Apps tab's side panel for the selected one;
/// <see cref="ToGroup"/> and <see cref="Apply"/> turn the text back into an <see cref="AppMatcher"/>, keeping the matcher
/// fields this form does not edit (path, class chain, full-screen rule); <see cref="SyncFrom"/> re-reads a stored group
/// after an undo or a rename in the tree. Nothing is validated here: the store's rules answer when the dialog confirms
/// or the panel applies.
/// </summary>
public sealed partial class GroupEditViewModel : ObservableObject
{
    private AppMatcher _matcher;

    public GroupEditViewModel()
        : this(AppMatcher.Empty)
    {
    }

    private GroupEditViewModel(AppMatcher matcher)
    {
        _matcher = matcher;
    }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>Windows executable file names, comma-separated: "chrome.exe, msedge.exe".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuessText))]
    public partial string WindowsNames { get; set; } = string.Empty;

    /// <summary>macOS executable file names, comma-separated: "Google Chrome, Safari".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuessText))]
    public partial string MacNames { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuessText))]
    public partial bool UseOnWindows { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuessText))]
    public partial bool UseOnMac { get; set; } = true;

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TitleIsRegex { get; set; }

    [ObservableProperty]
    public partial bool SuppressGlobals { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    /// <summary>
    /// What a platform left without names matches on: "macOS: Google Chrome (guessed)", "macOS: no guess, type its name or
    /// stop using the group there", nothing to say when every platform the group is used on has names of its own.
    /// </summary>
    public string GuessText
    {
        get
        {
            var matcher = Matcher();
            if (!matcher.HasProcessNames)
            {
                return "Type the executable's name for at least one platform.";
            }

            var parts = new List<string>(2);
            foreach (var platform in new[] { HostPlatform.Windows, HostPlatform.MacOS })
            {
                if (!UseOn.Includes(platform) || !matcher.IsGuessedOn(platform))
                {
                    continue;
                }

                var guess = matcher.EffectiveProcessNames(platform);
                parts.Add(guess.Count > 0
                    ? $"{PlatformName(platform)}: {string.Join(", ", guess)} (guessed)"
                    : $"{PlatformName(platform)}: no guess; type its name, or stop using the group there");
            }

            return parts.Count == 0 ? "None needed: every platform the group is used on has its own names." : string.Join(" · ", parts);
        }
    }

    private PlatformSet UseOn => PlatformSet.None.With(HostPlatform.Windows, UseOnWindows).With(HostPlatform.MacOS, UseOnMac);

    public static GroupEditViewModel From(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var edit = new GroupEditViewModel(group.Matcher ?? AppMatcher.Empty);
        edit.SyncFrom(group);
        return edit;
    }

    /// <summary>Takes the stored group's values; a property that already holds the value raises nothing.</summary>
    public void SyncFrom(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        _matcher = group.Matcher ?? AppMatcher.Empty;
        Name = group.Name;
        if (!_matcher.WindowsProcessNames.SequenceEqual(Split(WindowsNames), StringComparer.Ordinal))
        {
            WindowsNames = string.Join(", ", _matcher.WindowsProcessNames);
        }

        if (!_matcher.MacProcessNames.SequenceEqual(Split(MacNames), StringComparer.Ordinal))
        {
            MacNames = string.Join(", ", _matcher.MacProcessNames);
        }

        UseOnWindows = group.UseOn.Includes(HostPlatform.Windows);
        UseOnMac = group.UseOn.Includes(HostPlatform.MacOS);
        WindowTitle = _matcher.Title ?? string.Empty;
        TitleIsRegex = _matcher.TitleIsRegex;
        SuppressGlobals = group.SuppressGlobals;
        IsActive = group.IsActive;
    }

    /// <summary>A new group with these settings and no commands.</summary>
    public AppGroup ToGroup(GroupId id) => new(id, Name, IsActive, SuppressGlobals, Matcher(), []) { UseOn = UseOn };

    /// <summary>The existing group with these settings, its commands untouched.</summary>
    public AppGroup Apply(AppGroup existing)
    {
        ArgumentNullException.ThrowIfNull(existing);
        return existing with { Name = Name, IsActive = IsActive, SuppressGlobals = SuppressGlobals, UseOn = UseOn, Matcher = Matcher() };
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
        new Section("App identification",
        [
            new TextField("Windows executables", new DelegateBinding<string>(() => WindowsNames, value => WindowsNames = value, this), "Comma-separated; any of them matches: chrome.exe, msedge.exe"),
            new TextField("macOS executables", new DelegateBinding<string>(() => MacNames, value => MacNames = value, this), "Comma-separated; any of them matches: Google Chrome, Safari"),
            new TextField("Guess for an empty list", new DelegateBinding<string>(() => GuessText, owner: this), "Well-known apps have a name on each platform; typing names for a platform replaces the guess."),
            new NoteField("Pick a window", "A crosshair that fills the current platform's names from a window on screen comes in a later slice; type the executable name for now."),
        ], "The executable's file name per platform, as the log shows it after process=."),
        new Section("More matching options",
        [
            new TextField("Window title", new DelegateBinding<string>(() => WindowTitle, value => WindowTitle = value, this), "Exact, case-insensitive, unless the toggle below makes it a pattern."),
            new ToggleField("Title is a regular expression", new DelegateBinding<bool>(() => TitleIsRegex, value => TitleIsRegex = value, this)),
        ], "Rarely needed. Every filled field must match; empty fields are ignored."),
    ]);

    private static string PlatformName(HostPlatform platform) => platform == HostPlatform.MacOS ? "macOS" : "Windows";

    private static string[] Split(string names) => names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private AppMatcher Matcher() => _matcher with
    {
        WindowsProcessNames = Split(WindowsNames),
        MacProcessNames = Split(MacNames),
        Title = string.IsNullOrWhiteSpace(WindowTitle) ? null : WindowTitle.Trim(),
        TitleIsRegex = TitleIsRegex,
    };
}
