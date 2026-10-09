using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// How an app group or an ignored app recognises a window (F5 app identification). Every field that
/// is set must match (AND); fields left null or empty are not consulted; a matcher with nothing set
/// matches nothing, so a half-filled group is harmless. The executable names are the primary field
/// (F8, Joel 2026-10-07: per platform), any of which matches, compared case-insensitively (one group for all
/// Chromium browsers needs no regex): <see cref="WindowsProcessNames"/> on Windows, <see cref="MacProcessNames"/>
/// on macOS; with a list's regex toggle on, each of its entries is a pattern (Joel, 2026-10-09: <c>Spine(?:-1)?\.exe</c>).
/// A platform whose list is empty matches on the other list's best guess (<see cref="KnownApps"/>; none from a regex
/// list); a group that has names, but none for this platform and no guess, matches nothing here, never everything.
/// Every text field is exact (case-insensitive) unless its regex toggle is on; a regex is case-insensitive,
/// culture-invariant, unanchored, limited to 100 ms, and an invalid pattern is "no match" here and a validation error in
/// <see cref="MappingRules"/>. Besides this platform's path (<see cref="PathFor"/>) and the title (the root owner's, the window activation targets), the
/// StrokesPlus.net app definition's per-window fields (2026-10-09): the root's, the parent's and the control's title, and
/// the owner's, root's, parent's and control's class, read from <see cref="WindowIdentity.Levels"/> (Windows; on macOS
/// those are unknown, so a matcher that sets one matches nothing there). <see cref="ClassChain"/> is the older Windows
/// escape hatch, kept so configs that use it match as before: every listed class must appear somewhere in the window's
/// class chain; an entry may list alternatives with <c>|</c> ("Progman|WorkerW"). <see cref="IgnoreWhenFullScreen"/> is A21.
/// </summary>
public sealed record AppMatcher
{
    /// <summary>Windows executable file names ("chrome.exe"). The file's key is <c>processNames</c>: the format predates the macOS list.</summary>
    public IReadOnlyList<string> WindowsProcessNames { get; init; } = [];

    /// <summary>macOS executable file names ("Google Chrome"), as the log shows them after <c>process=</c>.</summary>
    public IReadOnlyList<string> MacProcessNames { get; init; } = [];

    /// <summary>Each of <see cref="WindowsProcessNames"/> is a pattern.</summary>
    public bool WindowsProcessNamesAreRegex { get; init; }

    /// <summary>Each of <see cref="MacProcessNames"/> is a pattern.</summary>
    public bool MacProcessNamesAreRegex { get; init; }

    /// <summary>The Windows executable's full path. The file's key is <c>processPath</c>: the format predates the macOS one.</summary>
    public string? ProcessPath { get; init; }

    public bool ProcessPathIsRegex { get; init; }

    /// <summary>
    /// The macOS executable's full path (2026-10-09): a path names one platform's file, so each platform has its own, as the
    /// executable names do; a group synced from the PC with a Windows path set no longer matches nothing on a Mac.
    /// </summary>
    public string? MacProcessPath { get; init; }

    public bool MacProcessPathIsRegex { get; init; }

    public string? Title { get; init; }

    public bool TitleIsRegex { get; init; }

    public string? RootTitle { get; init; }

    public bool RootTitleIsRegex { get; init; }

    public string? ParentTitle { get; init; }

    public bool ParentTitleIsRegex { get; init; }

    /// <summary>The caption of the control under the point (SP.net's "Title/Text").</summary>
    public string? ControlTitle { get; init; }

    public bool ControlTitleIsRegex { get; init; }

    public string? OwnerClass { get; init; }

    public bool OwnerClassIsRegex { get; init; }

    public string? RootClass { get; init; }

    public bool RootClassIsRegex { get; init; }

    public string? ParentClass { get; init; }

    public bool ParentClassIsRegex { get; init; }

    /// <summary>The class of the control under the point (SP.net's "Class Name").</summary>
    public string? ControlClass { get; init; }

    public bool ControlClassIsRegex { get; init; }

    public IReadOnlyList<string> ClassChain { get; init; } = [];

    public bool IgnoreWhenFullScreen { get; init; }

    /// <summary>Nothing set: a new group before its identification page is filled in. Matches nothing.</summary>
    public static AppMatcher Empty { get; } = new();

    public bool IsEmpty
        => !HasProcessNames && !HasText(ProcessPath) && !HasText(MacProcessPath) && !HasText(Title) && !ClassChain.Any(HasText) && !WindowFields.Any(entry => HasText(entry.Pattern));

    /// <summary>
    /// The per-window fields as (name for messages, pattern, regex, what the window has): the root, parent and control
    /// titles, then the owner, root, parent and control classes. The validation rules and the matching walk this one list.
    /// </summary>
    internal IEnumerable<(string Name, string? Pattern, bool IsRegex, Func<WindowLevels, string?> Actual)> WindowFields =>
    [
        ("root title", RootTitle, RootTitleIsRegex, levels => levels.RootTitle),
        ("parent title", ParentTitle, ParentTitleIsRegex, levels => levels.ParentTitle),
        ("control title", ControlTitle, ControlTitleIsRegex, levels => levels.ControlTitle),
        ("owner class", OwnerClass, OwnerClassIsRegex, levels => levels.OwnerClass),
        ("root class", RootClass, RootClassIsRegex, levels => levels.RootClass),
        ("parent class", ParentClass, ParentClassIsRegex, levels => levels.ParentClass),
        ("control class", ControlClass, ControlClassIsRegex, levels => levels.ControlClass),
    ];

    /// <summary><paramref name="platform"/>'s executable path and whether it is a pattern; only that one is consulted there.</summary>
    public (string? Path, bool IsRegex) PathFor(HostPlatform platform)
        => platform == HostPlatform.MacOS ? (MacProcessPath, MacProcessPathIsRegex) : (ProcessPath, ProcessPathIsRegex);

    /// <summary>Whether <paramref name="platform"/>'s list holds patterns.</summary>
    public bool ProcessNamesAreRegexOn(HostPlatform platform) => platform == HostPlatform.MacOS ? MacProcessNamesAreRegex : WindowsProcessNamesAreRegex;

    /// <summary>Either platform's list names an executable.</summary>
    public bool HasProcessNames => WindowsProcessNames.Any(HasText) || MacProcessNames.Any(HasText);

    public IReadOnlyList<string> ProcessNamesFor(HostPlatform platform) => platform == HostPlatform.MacOS ? MacProcessNames : WindowsProcessNames;

    /// <summary>True when <paramref name="platform"/>'s list is empty and the names it matches on are the guess from the other list.</summary>
    public bool IsGuessedOn(HostPlatform platform) => !ProcessNamesFor(platform).Any(HasText) && HasProcessNames;

    /// <summary>The names <paramref name="platform"/> matches on: its own list, else the known-app guess from the other list (never from patterns); empty when neither gives any.</summary>
    public IReadOnlyList<string> EffectiveProcessNames(HostPlatform platform)
    {
        var own = ProcessNamesFor(platform);
        if (own.Any(HasText))
        {
            return own;
        }

        var other = Other(platform);
        return ProcessNamesAreRegexOn(other) ? [] : KnownApps.Guess(ProcessNamesFor(other).Where(HasText), platform);
    }

    public bool Matches(WindowIdentity window, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (IsEmpty || (IgnoreWhenFullScreen && window.IsFullScreen))
        {
            return false;
        }

        return MatchesProcessName(window.ProcessName, platform)
            && MatchesText(PathFor(platform).Path, PathFor(platform).IsRegex, window.ProcessPath)
            && MatchesText(Title, TitleIsRegex, window.Title)
            && WindowFields.All(entry => MatchesText(entry.Pattern, entry.IsRegex, entry.Actual(window.Levels)))
            && MatchesClassChain(window.ClassChain);
    }

    private static HostPlatform Other(HostPlatform platform) => platform == HostPlatform.MacOS ? HostPlatform.Windows : HostPlatform.MacOS;

    /// <summary>Not consulted when no list names anything; otherwise the window's executable must be one of this platform's names or guesses.</summary>
    private bool MatchesProcessName(string actual, HostPlatform platform)
    {
        if (!HasProcessNames)
        {
            return true;
        }

        // A guess is always a plain name; only this platform's own list can hold patterns.
        var patterns = ProcessNamesFor(platform).Any(HasText) && ProcessNamesAreRegexOn(platform);
        return EffectiveProcessNames(platform).Any(name => HasText(name) && MatchesText(name, patterns, actual));
    }

    private static bool MatchesText(string? expected, bool isRegex, string? actual)
    {
        if (!HasText(expected))
        {
            return true;
        }

        if (actual is null)
        {
            return false;
        }

        return isRegex
            ? MatcherRegexCache.IsMatch(expected, actual)
            : string.Equals(expected.Trim(), actual, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesClassChain(IReadOnlyList<string> actual)
    {
        foreach (var wanted in ClassChain)
        {
            if (HasText(wanted) && !actual.Any(className => ClassMatches(wanted, className)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ClassMatches(string wanted, string actual)
    {
        foreach (var alternative in wanted.Split('|'))
        {
            if (string.Equals(alternative.Trim(), actual, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasText([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? text)
        => !string.IsNullOrWhiteSpace(text);
}
