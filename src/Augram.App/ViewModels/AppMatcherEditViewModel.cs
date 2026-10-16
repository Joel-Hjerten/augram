using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The app identification form, one for both hosts (F5 app identification, F8 per platform; plan 0001 M2 step 6): an app
/// group on Commands › Apps (<c>GroupEditViewModel</c>) and an ignored app on the Exclusions tab (<c>IgnoredEditViewModel</c>).
/// Laid out as StrokesPlus.net's App Definition (Joel, 2026-10-09): one row per field, each a text box, a magnifier and a
/// Use Regex check box (<see cref="PatternField"/>), under a Windows | macOS switch (<see cref="View"/>) that flips the same
/// rows between the two platforms' values, the other platform's read-only with "Edit on Windows" (plan 0010 decision 7): the executable names (comma-separated; the known-app guess shows in an empty
/// box), the executable path and the window title on both, the root, parent and control titles and the four classes on
/// Windows only (<c>.WindowDetails</c>), then the full-screen rule (A21) for both. Only Executable and the fields that hold
/// something show until "More options…" (<see cref="ShowsAllFields"/>; Joel, 2026-10-16). A magnifier shows only on this machine's
/// side, since only its windows are on screen (<c>.Picking</c>). <see cref="Sections"/> are its part of the host's
/// <see cref="FormScreen"/>; <see cref="ToMatcher"/> turns the text back into an <see cref="AppMatcher"/> (every matcher
/// field is on the form, so nothing is kept aside); <see cref="SyncFrom"/> re-reads a stored one without touching a list
/// whose names are the same (a trailing comma being typed survives). Nothing is validated here: the store's rules answer
/// when the host applies.
/// </summary>
public sealed partial class AppMatcherEditViewModel : ObservableObject
{
    public const string SectionTitle = "App identification";
    public const string PlatformSwitchLabel = "Fields for";

    /// <summary>The label of the other side's note ("Edit on Windows", <see cref="PlatformSideView.EditNote"/>; plan 0010 decision 7).</summary>
    public const string ReadOnlyLabel = "Read-only";

    /// <summary>The button that shows every field, and the one that hides the empty ones again (Joel, 2026-10-16).</summary>
    public const string MoreFieldsLabel = "More options…";
    public const string FewerFieldsLabel = "Fewer options";

    private readonly Func<PlatformSet> _usedOn;
    private readonly string _noGuessAdvice;
    private readonly string _noneNeeded;
    private readonly HashSet<(HostPlatform Side, string Label)> _revealed = [];
    private PlatformSideView? _view;

    /// <param name="usedOn">The platforms the host is used on: the guess is shown for those only.</param>
    /// <param name="noGuessAdvice">What to do about a platform without names or a guess ("type its name, or stop using the group there").</param>
    /// <param name="noneNeeded">The guess line when every such platform has names of its own.</param>
    public AppMatcherEditViewModel(Func<PlatformSet> usedOn, string noGuessAdvice, string noneNeeded)
    {
        ArgumentNullException.ThrowIfNull(usedOn);
        _usedOn = usedOn;
        _noGuessAdvice = noGuessAdvice;
        _noneNeeded = noneNeeded;
    }

    /// <summary>Which platform's fields show; opens on this machine's, and only those are edited here (plan 0010 decision 7). Not a matcher field.</summary>
    public PlatformSideView View => _view ??= new PlatformSideView(Platform);

    /// <summary>Windows executable file names, comma-separated: "chrome.exe, msedge.exe".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuessText))]
    public partial string WindowsNames { get; set; } = string.Empty;

    /// <summary>macOS executable file names, comma-separated: "Google Chrome, Safari".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuessText))]
    public partial string MacNames { get; set; } = string.Empty;

    /// <summary>Each Windows name is a pattern ("Spine(?:-1)?\.exe").</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuessText))]
    public partial bool WindowsNamesAreRegex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuessText))]
    public partial bool MacNamesAreRegex { get; set; }

    /// <summary>The Windows executable's full path; <see cref="MacProcessPath"/> is the macOS one.</summary>
    [ObservableProperty]
    public partial string ProcessPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PathIsRegex { get; set; }

    [ObservableProperty]
    public partial string MacProcessPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool MacPathIsRegex { get; set; }

    /// <summary>The Windows window title (the root owner's); <see cref="MacWindowTitle"/> is the macOS one.</summary>
    [ObservableProperty]
    public partial string WindowTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TitleIsRegex { get; set; }

    [ObservableProperty]
    public partial string MacWindowTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool MacTitleIsRegex { get; set; }

    /// <summary>The older window classes list, comma-separated; an entry may list alternatives with <c>|</c> ("Progman|WorkerW").</summary>
    [ObservableProperty]
    public partial string WindowClasses { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IgnoreWhenFullScreen { get; set; }

    /// <summary>
    /// Every field shows, the empty ones too (<see cref="MoreFieldsLabel"/>). Off, the side in view shows Executable and the
    /// fields that hold something (Joel, 2026-10-16: the empty rows clutter the form). View state, never an edit; another
    /// selection makes a new form, which starts with it off.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowsAllFields { get; set; }

    /// <summary>How many of the side in view's fields are hidden now: the hint beside <see cref="MoreFieldsLabel"/>.</summary>
    public int HiddenFieldCount => OptionalFields().Count(optional => !Reveals(optional.Label, optional.Holds()));

    /// <summary>Back to the short form: only the fields that hold something stay (<see cref="FewerFieldsLabel"/>).</summary>
    public void ShowFewerFields()
    {
        _revealed.Clear();
        if (ShowsAllFields)
        {
            ShowsAllFields = false;
        }
        else
        {
            // Already short: the rows still re-read, so an emptied field kept on screen goes.
            OnPropertyChanged(nameof(ShowsAllFields));
        }
    }

    /// <summary>
    /// What a platform left without names matches on: "macOS: Google Chrome (guessed)", "macOS: no guess; …" with the host's
    /// advice, or the host's "none needed" line when every platform it is used on has names of its own.
    /// </summary>
    public string GuessText
    {
        get
        {
            var matcher = ToMatcher();
            if (!matcher.HasProcessNames)
            {
                return "Type the executable's name for at least one platform.";
            }

            var parts = new[] { HostPlatform.Windows, HostPlatform.MacOS }
                .Select(platform => (platform, guess: GuessFor(matcher, platform)))
                .Where(entry => entry.guess.Length > 0)
                .Select(entry => $"{entry.platform.DisplayName()}: {entry.guess}")
                .ToList();
            return parts.Count == 0 ? _noneNeeded : string.Join(" · ", parts);
        }
    }

    /// <summary>The host's "used on" changed: the guess follows.</summary>
    public void UsedOnChanged() => OnPropertyChanged(nameof(GuessText));

    /// <summary>The grey text in <paramref name="platform"/>'s empty executable box: the guess, why there is none, or nothing.</summary>
    public string GuessFor(HostPlatform platform) => GuessFor(ToMatcher(), platform);

    /// <summary>Takes the stored matcher's values; a property that already holds the value raises nothing.</summary>
    public void SyncFrom(AppMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        if (!matcher.WindowsProcessNames.SequenceEqual(Split(WindowsNames), StringComparer.Ordinal))
        {
            WindowsNames = string.Join(", ", matcher.WindowsProcessNames);
        }

        if (!matcher.MacProcessNames.SequenceEqual(Split(MacNames), StringComparer.Ordinal))
        {
            MacNames = string.Join(", ", matcher.MacProcessNames);
        }

        if (!matcher.ClassChain.SequenceEqual(Split(WindowClasses), StringComparer.Ordinal))
        {
            WindowClasses = string.Join(", ", matcher.ClassChain);
        }

        WindowsNamesAreRegex = matcher.WindowsProcessNamesAreRegex;
        MacNamesAreRegex = matcher.MacProcessNamesAreRegex;
        ProcessPath = matcher.ProcessPath ?? string.Empty;
        PathIsRegex = matcher.ProcessPathIsRegex;
        MacProcessPath = matcher.MacProcessPath ?? string.Empty;
        MacPathIsRegex = matcher.MacProcessPathIsRegex;
        WindowTitle = matcher.Title ?? string.Empty;
        TitleIsRegex = matcher.TitleIsRegex;
        MacWindowTitle = matcher.MacTitle ?? string.Empty;
        MacTitleIsRegex = matcher.MacTitleIsRegex;
        SyncWindowFieldsFrom(matcher);
        IgnoreWhenFullScreen = matcher.IgnoreWhenFullScreen;
    }

    public AppMatcher ToMatcher() => WithWindowFields(new()
    {
        WindowsProcessNames = Split(WindowsNames),
        MacProcessNames = Split(MacNames),
        WindowsProcessNamesAreRegex = WindowsNamesAreRegex,
        MacProcessNamesAreRegex = MacNamesAreRegex,
        ProcessPath = Trimmed(ProcessPath),
        ProcessPathIsRegex = PathIsRegex,
        MacProcessPath = Trimmed(MacProcessPath),
        MacProcessPathIsRegex = MacPathIsRegex,
        Title = Trimmed(WindowTitle),
        TitleIsRegex = TitleIsRegex,
        MacTitle = Trimmed(MacWindowTitle),
        MacTitleIsRegex = MacTitleIsRegex,
        ClassChain = Split(WindowClasses),
        IgnoreWhenFullScreen = IgnoreWhenFullScreen,
    });

    /// <summary>
    /// The form's part of the host's screen: the Windows | macOS switch, then one row per field for the platform it shows
    /// (the Windows-only rows hidden on the macOS side), then the full-screen rule.
    /// </summary>
    public IReadOnlyList<Section> Sections()
    {
        var changes = new FormChanges(this, View);
        bool Mac() => View.Shown == HostPlatform.MacOS;
        var here = new DelegateBinding<bool>(() => View.IsHere, owner: View, propertyName: null);
        return
        [
            new Section(SectionTitle,
            [
                new ButtonRadioField<HostPlatform>(PlatformSwitchLabel, [new(HostPlatform.Windows.DisplayName(), HostPlatform.Windows, View.Here == HostPlatform.Windows), new(HostPlatform.MacOS.DisplayName(), HostPlatform.MacOS, View.Here == HostPlatform.MacOS)],
                    new DelegateBinding<HostPlatform>(() => View.Shown, value => View.Shown = value, View),
                    "Each platform has its own fields: a Windows field never applies on a Mac, nor a macOS one on Windows. Only this computer's are edited here; the other platform's show read-only."),
                new NoteField(ReadOnlyLabel, new DelegateBinding<string>(() => PlatformSideView.EditNote(View.Shown), owner: View, propertyName: null))
                {
                    Visible = new DelegateBinding<bool>(() => !View.IsHere, owner: View, propertyName: null),
                },
                new PatternField("Executable",
                    new DelegateBinding<string>(() => Mac() ? MacNames : WindowsNames, value => { if (Mac()) { MacNames = value; } else { WindowsNames = value; } }, changes, propertyName: null),
                    new DelegateBinding<bool>(() => Mac() ? MacNamesAreRegex : WindowsNamesAreRegex, value => { if (Mac()) { MacNamesAreRegex = value; } else { WindowsNamesAreRegex = value; } }, changes, propertyName: null),
                    "File names, comma-separated; any of them matches: chrome.exe, msedge.exe (Google Chrome, Safari on a Mac).")
                {
                    Finder = WindowFinderAccessory.Finder(window => window.ProcessName, window => TakeExecutable(window)),
                    FinderVisible = here,
                    Enabled = here,
                    Placeholder = new DelegateBinding<string>(() => GuessFor(View.Shown), owner: changes, propertyName: null),
                },
                new PatternField("Executable path",
                    new DelegateBinding<string>(() => Mac() ? MacProcessPath : ProcessPath, value => { if (Mac()) { MacProcessPath = value; } else { ProcessPath = value; } }, changes, propertyName: null),
                    new DelegateBinding<bool>(() => Mac() ? MacPathIsRegex : PathIsRegex, value => { if (Mac()) { MacPathIsRegex = value; } else { PathIsRegex = value; } }, changes, propertyName: null),
                    "The executable's full path.")
                {
                    Finder = WindowFinderAccessory.Finder(PathOf, TakePath),
                    FinderVisible = here,
                    Enabled = here,
                    Visible = Revealed(changes, PathLabel, HoldsPath),
                },
                new PatternField("Window title",
                    new DelegateBinding<string>(() => Mac() ? MacWindowTitle : WindowTitle, value => { if (Mac()) { MacWindowTitle = value; } else { WindowTitle = value; } }, changes, propertyName: null),
                    new DelegateBinding<bool>(() => Mac() ? MacTitleIsRegex : TitleIsRegex, value => { if (Mac()) { MacTitleIsRegex = value; } else { TitleIsRegex = value; } }, changes, propertyName: null),
                    "The main window's title (StrokesPlus.net's owner title).")
                {
                    Finder = WindowFinderAccessory.Finder(TitleOf, TakeTitle),
                    FinderVisible = here,
                    Enabled = here,
                    Visible = Revealed(changes, TitleLabel, HoldsTitle),
                },
                .. WindowFieldRows(),
                new ButtonsField("Other fields", [new FieldButton(MoreFieldsLabel, () => ShowsAllFields = true)], "Only Executable and the fields that hold something show; the rest are empty, and empty fields are ignored.")
                {
                    Visible = new DelegateBinding<bool>(() => !ShowsAllFields && HiddenFieldCount > 0, owner: changes, propertyName: null),
                    Hint = new DelegateBinding<string>(() => HiddenFieldCount == 1 ? "1 empty field hidden" : $"{HiddenFieldCount} empty fields hidden", owner: changes, propertyName: null),
                },
                new ButtonsField("Other fields", [new FieldButton(FewerFieldsLabel, ShowFewerFields)], "Hides the empty fields again.")
                {
                    Visible = new DelegateBinding<bool>(() => ShowsAllFields, owner: changes, propertyName: null),
                },
                new ToggleField("Not when full screen", new DelegateBinding<bool>(() => IgnoreWhenFullScreen, value => IgnoreWhenFullScreen = value, this), "Both platforms: matches nothing while the window covers its whole screen."),
            ], "Every filled field must match; empty fields are ignored. Exact and case-insensitive unless Use Regex makes the field a pattern."),
        ];
    }

    private const string PathLabel = "Executable path";
    private const string TitleLabel = "Window title";

    private bool HoldsPath() => View.Shown == HostPlatform.MacOS ? Holds(MacProcessPath, MacPathIsRegex) : Holds(ProcessPath, PathIsRegex);

    private bool HoldsTitle() => View.Shown == HostPlatform.MacOS ? Holds(MacWindowTitle, MacTitleIsRegex) : Holds(WindowTitle, TitleIsRegex);

    private static bool Holds(string text, bool isRegex) => isRegex || !string.IsNullOrWhiteSpace(text);

    /// <summary>The side in view's fields that hide while empty: the path and the title, and on Windows the per-window fields.</summary>
    private IEnumerable<(string Label, Func<bool> Holds)> OptionalFields()
    {
        yield return (PathLabel, HoldsPath);
        yield return (TitleLabel, HoldsTitle);
        if (View.ShowsWindows)
        {
            foreach (var field in WindowFields)
            {
                yield return (field.Label, () => Holds(field.Text(), field.IsRegex()));
            }
        }
    }

    private DelegateBinding<bool> Revealed(FormChanges changes, string label, Func<bool> holds)
        => new(() => Reveals(label, holds()), owner: changes, propertyName: null);

    /// <summary>
    /// Whether a field shows on the side in view: all do while <see cref="ShowsAllFields"/>; otherwise one that holds something,
    /// and one that held something since the form opened or was last shortened, so a field emptied to be retyped stays put.
    /// </summary>
    private bool Reveals(string label, bool holds)
    {
        var key = (View.Shown, label);
        if (holds)
        {
            _revealed.Add(key);
        }

        return ShowsAllFields || _revealed.Contains(key);
    }

    private string GuessFor(AppMatcher matcher, HostPlatform platform)
    {
        var summary = matcher.Summary(platform);
        if (!_usedOn().Includes(platform) || !summary.IsGuessed)
        {
            return string.Empty;
        }

        return summary.Names.Count > 0 ? summary.NamesText : $"no guess; {_noGuessAdvice}";
    }

    private static string[] Split(string names) => names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Trimmed(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
