using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// What an <see cref="AppMatcher"/> matches on one platform, the one reading every list and form summarises from
/// (<see cref="AppMatcher.Summary"/>, plan 0007): the names it matches on there (<see cref="AppMatcher.EffectiveProcessNames"/>)
/// and whether they are the known-app guess, whether it can match there at all (<see cref="IgnoreList.CanMatchOn"/>), and the
/// window detail it falls back on. <see cref="Text"/> is the Exclusions row's words ("chrome.exe", "Google Chrome (guessed)",
/// "matches nothing yet", "nothing on macOS"); <see cref="NamesText"/> the identification form's guess; <see cref="LacksNameHere"/>
/// the Apps tab's "no macOS name".
/// </summary>
public sealed record AppMatcherSummary(HostPlatform Platform, bool IsEmpty, bool HasProcessNames, bool CanMatch, IReadOnlyList<string> Names, bool IsGuessed, string? Detail)
{
    /// <summary>"chrome.exe, msedge.exe", "Google Chrome (guessed)" when they are the guess; empty for no names.</summary>
    public string NamesText => Names.Count == 0 ? string.Empty : IsGuessed ? string.Join(", ", Names) + " (guessed)" : string.Join(", ", Names);

    /// <summary>The matcher has executable names, but neither its own nor a guess for this platform: it matches nothing here.</summary>
    public bool LacksNameHere => HasProcessNames && Names.Count == 0;

    /// <summary>"matches nothing yet", "nothing on macOS", the names (<see cref="NamesText"/>), else the title or a path, else "window details".</summary>
    public string Text
        => IsEmpty ? "matches nothing yet"
            : !CanMatch ? "nothing on " + Platform.DisplayName()
            : HasProcessNames ? NamesText
            : Detail ?? "window details";

    public static AppMatcherSummary Of(AppMatcher matcher, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        return new AppMatcherSummary(
            platform,
            matcher.IsEmpty,
            matcher.HasProcessNames,
            IgnoreList.CanMatchOn(matcher, platform),
            matcher.EffectiveProcessNames(platform),
            matcher.IsGuessedOn(platform),
            matcher.Title ?? matcher.PathFor(platform).Path ?? matcher.ProcessPath ?? matcher.MacProcessPath);
    }
}
