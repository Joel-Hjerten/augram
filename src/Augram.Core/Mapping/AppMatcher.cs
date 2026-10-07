using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// How an app group or an ignored app recognises a window (F5 app identification). Every field that
/// is set must match (AND); fields left null or empty are not consulted; a matcher with nothing set
/// matches nothing, so a half-filled group is harmless. <see cref="ProcessNames"/> is the primary
/// field: executable file names, any of which matches, compared case-insensitively (one group for all
/// Chromium browsers needs no regex). Path and title are exact (case-insensitive) unless their regex
/// toggle is on; a regex is case-insensitive, culture-invariant, limited to 100 ms, and an invalid
/// pattern is "no match" here and a validation error in <see cref="MappingRules"/>.
/// <see cref="ClassChain"/> is the Windows escape hatch: every listed class must appear somewhere in
/// the window's class chain; an entry may list alternatives with <c>|</c> ("Progman|WorkerW"), the
/// SP.net idiom for the desktop. <see cref="IgnoreWhenFullScreen"/> is A21.
/// </summary>
public sealed record AppMatcher
{
    public IReadOnlyList<string> ProcessNames { get; init; } = [];

    public string? ProcessPath { get; init; }

    public bool ProcessPathIsRegex { get; init; }

    public string? Title { get; init; }

    public bool TitleIsRegex { get; init; }

    public IReadOnlyList<string> ClassChain { get; init; } = [];

    public bool IgnoreWhenFullScreen { get; init; }

    /// <summary>Nothing set: a new group before its identification page is filled in. Matches nothing.</summary>
    public static AppMatcher Empty { get; } = new();

    public bool IsEmpty
        => !ProcessNames.Any(HasText) && !HasText(ProcessPath) && !HasText(Title) && !ClassChain.Any(HasText);

    public bool Matches(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (IsEmpty || (IgnoreWhenFullScreen && window.IsFullScreen))
        {
            return false;
        }

        return MatchesProcessName(window.ProcessName)
            && MatchesText(ProcessPath, ProcessPathIsRegex, window.ProcessPath)
            && MatchesText(Title, TitleIsRegex, window.Title)
            && MatchesClassChain(window.ClassChain);
    }

    private bool MatchesProcessName(string actual)
    {
        bool any = false;
        foreach (var name in ProcessNames)
        {
            if (!HasText(name))
            {
                continue;
            }

            any = true;
            if (string.Equals(name.Trim(), actual, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return !any;
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
