using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// How an app group or an ignored app recognises a window (F5 app identification). Every field that
/// is set must match (AND); fields left null or empty are not consulted; a matcher with nothing set
/// matches nothing, so a half-filled group is harmless. The executable names are the primary field
/// (F8, Joel 2026-10-07: per platform), any of which matches, compared case-insensitively (one group for all
/// Chromium browsers needs no regex): <see cref="WindowsProcessNames"/> on Windows, <see cref="MacProcessNames"/>
/// on macOS. A platform whose list is empty matches on the other list's best guess (<see cref="KnownApps"/>); a group
/// that has names, but none for this platform and no guess, matches nothing here, never everything. Path and title are exact (case-insensitive) unless their regex
/// toggle is on; a regex is case-insensitive, culture-invariant, limited to 100 ms, and an invalid
/// pattern is "no match" here and a validation error in <see cref="MappingRules"/>.
/// <see cref="ClassChain"/> is the Windows escape hatch: every listed class must appear somewhere in
/// the window's class chain; an entry may list alternatives with <c>|</c> ("Progman|WorkerW"), the
/// SP.net idiom for the desktop. <see cref="IgnoreWhenFullScreen"/> is A21.
/// </summary>
public sealed record AppMatcher
{
    /// <summary>Windows executable file names ("chrome.exe"). The file's key is <c>processNames</c>: the format predates the macOS list.</summary>
    public IReadOnlyList<string> WindowsProcessNames { get; init; } = [];

    /// <summary>macOS executable file names ("Google Chrome"), as the log shows them after <c>process=</c>.</summary>
    public IReadOnlyList<string> MacProcessNames { get; init; } = [];

    public string? ProcessPath { get; init; }

    public bool ProcessPathIsRegex { get; init; }

    public string? Title { get; init; }

    public bool TitleIsRegex { get; init; }

    public IReadOnlyList<string> ClassChain { get; init; } = [];

    public bool IgnoreWhenFullScreen { get; init; }

    /// <summary>Nothing set: a new group before its identification page is filled in. Matches nothing.</summary>
    public static AppMatcher Empty { get; } = new();

    public bool IsEmpty
        => !HasProcessNames && !HasText(ProcessPath) && !HasText(Title) && !ClassChain.Any(HasText);

    /// <summary>Either platform's list names an executable.</summary>
    public bool HasProcessNames => WindowsProcessNames.Any(HasText) || MacProcessNames.Any(HasText);

    public IReadOnlyList<string> ProcessNamesFor(HostPlatform platform) => platform == HostPlatform.MacOS ? MacProcessNames : WindowsProcessNames;

    /// <summary>True when <paramref name="platform"/>'s list is empty and the names it matches on are the guess from the other list.</summary>
    public bool IsGuessedOn(HostPlatform platform) => !ProcessNamesFor(platform).Any(HasText) && HasProcessNames;

    /// <summary>The names <paramref name="platform"/> matches on: its own list, else the known-app guess from the other list; empty when neither gives any.</summary>
    public IReadOnlyList<string> EffectiveProcessNames(HostPlatform platform)
    {
        var own = ProcessNamesFor(platform);
        return own.Any(HasText) ? own : KnownApps.Guess(ProcessNamesFor(Other(platform)).Where(HasText), platform);
    }

    public bool Matches(WindowIdentity window, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (IsEmpty || (IgnoreWhenFullScreen && window.IsFullScreen))
        {
            return false;
        }

        return MatchesProcessName(window.ProcessName, platform)
            && MatchesText(ProcessPath, ProcessPathIsRegex, window.ProcessPath)
            && MatchesText(Title, TitleIsRegex, window.Title)
            && MatchesClassChain(window.ClassChain);
    }

    private static HostPlatform Other(HostPlatform platform) => platform == HostPlatform.MacOS ? HostPlatform.Windows : HostPlatform.MacOS;

    /// <summary>Not consulted when no list names anything; otherwise the window's executable must be one of this platform's names or guesses.</summary>
    private bool MatchesProcessName(string actual, HostPlatform platform)
        => !HasProcessNames
            || EffectiveProcessNames(platform).Any(name => HasText(name) && string.Equals(name.Trim(), actual, StringComparison.OrdinalIgnoreCase));

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
