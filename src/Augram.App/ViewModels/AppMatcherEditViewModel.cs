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
/// host says it is used on) and what the identified window offers besides, then, out of the way, the executable path and the
/// window title with their regex toggles, the window classes (Windows) and the full-screen rule (A21); this platform's
/// executables, the path, the title and the classes each have a magnifier of their own. <see cref="Sections"/> are its part
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

    [ObservableProperty]
    public partial string ProcessPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PathIsRegex { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TitleIsRegex { get; set; }

    /// <summary>Window classes, comma-separated; an entry may list alternatives with <c>|</c> ("Progman|WorkerW").</summary>
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

        ProcessPath = matcher.ProcessPath ?? string.Empty;
        PathIsRegex = matcher.ProcessPathIsRegex;
        WindowTitle = matcher.Title ?? string.Empty;
        TitleIsRegex = matcher.TitleIsRegex;
        IgnoreWhenFullScreen = matcher.IgnoreWhenFullScreen;
    }

    public AppMatcher ToMatcher() => new()
    {
        WindowsProcessNames = Split(WindowsNames),
        MacProcessNames = Split(MacNames),
        ProcessPath = Trimmed(ProcessPath),
        ProcessPathIsRegex = PathIsRegex,
        Title = Trimmed(WindowTitle),
        TitleIsRegex = TitleIsRegex,
        ClassChain = Split(WindowClasses),
        IgnoreWhenFullScreen = IgnoreWhenFullScreen,
    };

    /// <summary>
    /// The form's part of the host's screen: the identification first, the rarely needed fields after it. A magnifier sits
    /// before "Identify window", this platform's executables, the path, the title and (Windows) the classes; the identified
    /// window's path, title and classes show, each with Use, once a window was identified.
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
            new TextField("macOS executables", new DelegateBinding<string>(() => MacNames, value => MacNames = value, this), "Comma-separated; any of them matches: Google Chrome, Safari")
            {
                Accessory = ExecutableFinder(HostPlatform.MacOS),
            },
            new TextField("Guess for an empty list", new DelegateBinding<string>(() => GuessText, owner: this), "Well-known apps have a name on each platform; typing names for a platform replaces the guess."),
            new NoteField("Its path", new DelegateBinding<string>(() => Identified.PathText, owner: Identified), "The identified window's executable; Use makes it the executable path below.")
            {
                Visible = new DelegateBinding<bool>(() => Identified.HasPath, owner: Identified),
                Accessory = WindowFinderAccessory.UseButton(UseIdentifiedPath),
            },
            new NoteField("Its title", new DelegateBinding<string>(() => Identified.TitleText, owner: Identified), "Use makes it the window title below.")
            {
                Visible = new DelegateBinding<bool>(() => Identified.HasTitle, owner: Identified),
                Accessory = WindowFinderAccessory.UseButton(UseIdentifiedTitle),
            },
            new NoteField("Its classes", new DelegateBinding<string>(() => Identified.ClassesText, owner: Identified), "Use makes them the window classes below.")
            {
                Visible = new DelegateBinding<bool>(() => Identified.HasClasses, owner: Identified),
                Accessory = WindowFinderAccessory.UseButton(UseIdentifiedClasses),
            },
        ], "The executable's file name per platform, as the log shows it after process=."),
        new Section("More matching options",
        [
            new TextField("Executable path", new DelegateBinding<string>(() => ProcessPath, value => ProcessPath = value, this), "The executable's full path; exact, case-insensitive, unless the toggle below makes it a pattern.")
            {
                Accessory = WindowFinderAccessory.Finder(PathOf, TakePath),
            },
            new ToggleField("Path is a regular expression", new DelegateBinding<bool>(() => PathIsRegex, value => PathIsRegex = value, this)),
            new TextField("Window title", new DelegateBinding<string>(() => WindowTitle, value => WindowTitle = value, this), "Exact, case-insensitive, unless the toggle below makes it a pattern.")
            {
                Accessory = WindowFinderAccessory.Finder(TitleOf, TakeTitle),
            },
            new ToggleField("Title is a regular expression", new DelegateBinding<bool>(() => TitleIsRegex, value => TitleIsRegex = value, this)),
            new TextField("Window classes", new DelegateBinding<string>(() => WindowClasses, value => WindowClasses = value, this), "Windows only. Comma-separated; each must be in the window's class chain; Progman|WorkerW lists alternatives.")
            {
                Accessory = Platform == HostPlatform.Windows ? WindowFinderAccessory.Finder(ClassesOf, TakeClasses) : null,
            },
            new ToggleField("Not when full screen", new DelegateBinding<bool>(() => IgnoreWhenFullScreen, value => IgnoreWhenFullScreen = value, this), "Matches nothing while the window covers its whole screen."),
        ], "Rarely needed. Every filled field must match; empty fields are ignored."),
    ];

    private static string PlatformName(HostPlatform platform) => platform == HostPlatform.MacOS ? "macOS" : "Windows";

    private static string[] Split(string names) => names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Trimmed(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
