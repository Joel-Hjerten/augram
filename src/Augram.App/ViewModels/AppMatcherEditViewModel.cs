using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.ViewModels;

/// <summary>
/// The app identification form, one for both hosts (F5 app identification, F8 per platform; plan 0001 M2 step 6): an app
/// group on Commands › Apps (<c>GroupEditViewModel</c>) and an ignored app on the Ignored tab (<c>IgnoredEditViewModel</c>).
/// "Identify window" (a magnifier dragged onto a window: <c>.Picking</c>), the Windows and the macOS executable names as
/// comma-separated text with the known-app guess for a platform left empty (<see cref="GuessText"/>, over the platforms the
/// host says it is used on), each list with its regex toggle (2026-10-09), and what the identified window offers besides,
/// then, out of the way, the executable path and the window title with their regex toggles and the full-screen rule (A21),
/// and StrokesPlus.net's per-window fields (<c>.WindowDetails</c>, Windows); this platform's executables, the path, the
/// title and each per-window field have a magnifier of their own. <see cref="Sections"/> are its part
/// of the host's <see cref="FormScreen"/>; <see cref="ToMatcher"/> turns the text back into an <see cref="AppMatcher"/> (every
/// matcher field is on the form, so nothing is kept aside); <see cref="SyncFrom"/> re-reads a stored one without touching a
/// list whose names are the same (a trailing comma being typed survives). Nothing is validated here: the store's rules
/// answer when the host applies.
/// </summary>
public sealed partial class AppMatcherEditViewModel : ObservableObject
{
    private readonly Func<PlatformSet> _usedOn;
    private readonly string _noGuessAdvice;
    private readonly string _noneNeeded;

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

    [ObservableProperty]
    public partial string ProcessPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PathIsRegex { get; set; }

    /// <summary>The macOS executable's full path; <see cref="ProcessPath"/> is the Windows one.</summary>
    [ObservableProperty]
    public partial string MacProcessPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool MacPathIsRegex { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TitleIsRegex { get; set; }

    /// <summary>The older window classes list, comma-separated; an entry may list alternatives with <c>|</c> ("Progman|WorkerW").</summary>
    [ObservableProperty]
    public partial string WindowClasses { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IgnoreWhenFullScreen { get; set; }

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

            var usedOn = _usedOn();
            var parts = new List<string>(2);
            foreach (var platform in new[] { HostPlatform.Windows, HostPlatform.MacOS })
            {
                if (!usedOn.Includes(platform) || !matcher.IsGuessedOn(platform))
                {
                    continue;
                }

                var guess = matcher.EffectiveProcessNames(platform);
                parts.Add(guess.Count > 0
                    ? $"{PlatformName(platform)}: {string.Join(", ", guess)} (guessed)"
                    : $"{PlatformName(platform)}: no guess; {_noGuessAdvice}");
            }

            return parts.Count == 0 ? _noneNeeded : string.Join(" · ", parts);
        }
    }

    /// <summary>The host's "used on" changed: the guess line follows.</summary>
    public void UsedOnChanged() => OnPropertyChanged(nameof(GuessText));

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
        ClassChain = Split(WindowClasses),
        IgnoreWhenFullScreen = IgnoreWhenFullScreen,
    });

    /// <summary>
    /// The form's part of the host's screen: the identification first, the rarely needed fields after it, the per-window
    /// fields last. A magnifier sits before "Identify window", this platform's executables, the path, the title and (Windows)
    /// each per-window field; the identified window's path, title and per-window values show, each with Use, once a window
    /// was identified.
    /// </summary>
    public IReadOnlyList<Section> Sections() =>
    [
        new Section("App identification",
        [
            new NoteField("Identify window", new DelegateBinding<string>(() => Identified.Summary, owner: Identified), "Drag the magnifier onto any window: its executable joins this platform's list; its path, title and classes show below.")
            {
                Accessory = WindowFinderAccessory.Finder(WindowFinder.Summary, IdentifyWindow),
            },
            new TextField("Windows executables", new DelegateBinding<string>(() => WindowsNames, value => WindowsNames = value, this), "Comma-separated; any of them matches: chrome.exe, msedge.exe")
            {
                Accessory = ExecutableFinder(HostPlatform.Windows),
            },
            new ToggleField("Windows names are regular expressions", new DelegateBinding<bool>(() => WindowsNamesAreRegex, value => WindowsNamesAreRegex = value, this), "Each name is a pattern: Spine(?:-1)?\\.exe. Patterns give the other platform no guess."),
            new TextField("macOS executables", new DelegateBinding<string>(() => MacNames, value => MacNames = value, this), "Comma-separated; any of them matches: Google Chrome, Safari")
            {
                Accessory = ExecutableFinder(HostPlatform.MacOS),
            },
            new ToggleField("macOS names are regular expressions", new DelegateBinding<bool>(() => MacNamesAreRegex, value => MacNamesAreRegex = value, this)),
            new TextField("Guess for an empty list", new DelegateBinding<string>(() => GuessText, owner: this), "Well-known apps have a name on each platform; typing names for a platform replaces the guess."),
            new NoteField("Its path", new DelegateBinding<string>(() => Identified.PathText, owner: Identified), "The identified window's executable; Use makes it this platform's executable path below.")
            {
                Visible = new DelegateBinding<bool>(() => Identified.HasPath, owner: Identified),
                Accessory = WindowFinderAccessory.UseButton(UseIdentifiedPath),
            },
            new NoteField("Its title", new DelegateBinding<string>(() => Identified.TitleText, owner: Identified), "Use makes it the window title below.")
            {
                Visible = new DelegateBinding<bool>(() => Identified.HasTitle, owner: Identified),
                Accessory = WindowFinderAccessory.UseButton(UseIdentifiedTitle),
            },
            .. IdentifiedWindowFieldRows(),
        ], "The executable's file name per platform, as the log shows it after process=."),
        new Section("More matching options",
        [
            new TextField("Windows executable path", new DelegateBinding<string>(() => ProcessPath, value => ProcessPath = value, this), "The executable's full path on Windows; exact, case-insensitive, unless the toggle below makes it a pattern. A Mac never looks at it.")
            {
                Accessory = Platform == HostPlatform.Windows ? WindowFinderAccessory.Finder(PathOf, TakePath) : null,
            },
            new ToggleField("Windows path is a regular expression", new DelegateBinding<bool>(() => PathIsRegex, value => PathIsRegex = value, this)),
            new TextField("macOS executable path", new DelegateBinding<string>(() => MacProcessPath, value => MacProcessPath = value, this), "The executable's full path on a Mac: /Applications/Google Chrome.app/Contents/MacOS/Google Chrome. Windows never looks at it.")
            {
                Accessory = Platform == HostPlatform.MacOS ? WindowFinderAccessory.Finder(PathOf, TakePath) : null,
            },
            new ToggleField("macOS path is a regular expression", new DelegateBinding<bool>(() => MacPathIsRegex, value => MacPathIsRegex = value, this)),
            new TextField("Window title", new DelegateBinding<string>(() => WindowTitle, value => WindowTitle = value, this), "The main window's title (StrokesPlus.net's owner title). Exact, case-insensitive, unless the toggle below makes it a pattern.")
            {
                Accessory = WindowFinderAccessory.Finder(TitleOf, TakeTitle),
            },
            new ToggleField("Title is a regular expression", new DelegateBinding<bool>(() => TitleIsRegex, value => TitleIsRegex = value, this)),
            new ToggleField("Not when full screen", new DelegateBinding<bool>(() => IgnoreWhenFullScreen, value => IgnoreWhenFullScreen = value, this), "Matches nothing while the window covers its whole screen."),
        ], "Rarely needed. Every filled field must match; empty fields are ignored."),
        WindowDetailsSection(),
    ];

    private static string PlatformName(HostPlatform platform) => platform == HostPlatform.MacOS ? "macOS" : "Windows";

    private static string[] Split(string names) => names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Trimmed(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
