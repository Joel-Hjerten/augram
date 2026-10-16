using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// The ignore list's rules (F5; SP.net's Ignore List, reference §8 and §9), pure. Both modes make the stroke button pass
/// through untouched over a window of the app (<see cref="Under"/>; SP.net's plain ignored app); an app with
/// <see cref="IgnoredApp.DisableEntirely"/> also pauses Augram entirely while it has focus (<see cref="PausedBy"/>; SP.net's
/// "Disable S+ if this App Gains Focus", Joel's VMware, so the guest gets the raw button). The engine's ignore watch asks these
/// off the hook thread and publishes the answers the hook reads at button-down; <see cref="CommandResolver"/>'s
/// <see cref="ResolutionOutcome.Ignored"/> stays as the backstop for an answer that was stale. Inactive entries, and entries
/// that can match nothing on this platform, are invisible here, and so are Per command entries (plan 0004): they stop only
/// the commands that name them (<see cref="PerCommandUnder"/>), and While holding entries (plan 0012): they stop only the
/// presses made while one of their inputs is held (<see cref="HeldInputsUnder"/>).
/// </summary>
public static class IgnoreList
{
    /// <summary>An active ignored app (either mode) can match a window on <paramref name="platform"/>: the pointer is worth watching.</summary>
    public static bool WatchesPointer(MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return mapping.Ignored.Any(app => app.IsActive && app.IsGlobal && CanMatchOn(app.Matcher, platform));
    }

    /// <summary>An active "disable while focused" app can match a window on <paramref name="platform"/>: the foreground is worth watching.</summary>
    public static bool WatchesFocus(MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return mapping.Ignored.Any(app => app.IsActive && app.IsGlobal && app.DisableEntirely && CanMatchOn(app.Matcher, platform));
    }

    /// <summary>
    /// The first active ignored app on Ignored › Global (either mode) that claims the window under the pointer, or null: its
    /// stroke button passes through. The engine's ignore-list watch asks it as the pointer moves, so the stroke button passes
    /// through over the app before any stroke; <see cref="CommandResolver"/> asks it again as the backstop for a stroke captured
    /// on a stale answer. A Per command entry stops only the commands that name it (plan 0004, <see cref="PerCommandUnder"/>).
    /// </summary>
    public static IgnoredApp? Under(MappingDocument mapping, WindowIdentity? window, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return Claiming(mapping, window, platform, static app => app.IsGlobal).FirstOrDefault();
    }

    /// <summary>The first active "disable while focused" app that claims the focused window, or null: while it has focus Augram behaves as if disabled.</summary>
    public static IgnoredApp? PausedBy(MappingDocument mapping, WindowIdentity? focused, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return Claiming(mapping, focused, platform, static app => app.IsGlobal && app.DisableEntirely).FirstOrDefault();
    }

    /// <summary>
    /// Some command names an active Per command entry that can match a window on <paramref name="platform"/> in its "Not in"
    /// (plan 0004): the anchor plan over a window then depends on those entries as well as on its app group.
    /// </summary>
    public static bool UsesPerCommand(MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        var live = mapping.Ignored.Where(app => app.IsActive && app.IsPerCommand && CanMatchOn(app.Matcher, platform)).Select(app => app.Id).ToHashSet();
        return live.Count > 0 && mapping.Groups.Any(group => group.Commands.Any(command => command.NotIn.Any(live.Contains)));
    }

    /// <summary>
    /// The ids of the active Per command entries that claim the window (plan 0004), in list order; empty for no window. A command
    /// whose "Not in" names one of them is not used over it (<see cref="Command.IsNotIn"/>).
    /// </summary>
    public static IReadOnlyList<GroupId> PerCommandUnder(MappingDocument mapping, WindowIdentity? window, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        List<GroupId>? under = null;
        foreach (var app in Claiming(mapping, window, platform, static app => app.IsPerCommand))
        {
            (under ??= []).Add(app.Id);
        }

        return under ?? (IReadOnlyList<GroupId>)[];
    }

    /// <summary>
    /// An active Exclusions › While holding entry (plan 0012) with an input that counts on <paramref name="platform"/> can match a
    /// window there: the pointer's answer then carries <see cref="HeldInputsUnder"/>.
    /// </summary>
    public static bool WatchesHolding(MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return mapping.Ignored.Any(app => app.IsActive && app.IsWhileHolding && app.HeldInputsOn(platform).Count > 0 && CanMatchOn(app.Matcher, platform));
    }

    /// <summary>
    /// The inputs of the active While holding entries that claim the window (plan 0012 decisions 3 and 9: the window under the
    /// pointer at the press; several entries add up), as they count on <paramref name="platform"/> (<see cref="IgnoredApp.HeldInputsOn"/>);
    /// <see cref="WhileHoldingSet.None"/> for no window or none. Holding any of them at a press makes it pass through, beside the
    /// global ignore keys. The engine's ignore-list watch asks it off the hook thread.
    /// </summary>
    public static WhileHoldingSet HeldInputsUnder(MappingDocument mapping, WindowIdentity? window, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        List<KeyOrButton>? inputs = null;
        foreach (var app in Claiming(mapping, window, platform, static app => app.IsWhileHolding))
        {
            (inputs ??= []).AddRange(app.HeldInputsOn(platform));
        }

        return inputs is null ? WhileHoldingSet.None : WhileHoldingSet.Of(inputs);
    }

    /// <summary>The one loop behind the lookups above: the active entries of <paramref name="kind"/> whose matcher claims <paramref name="window"/>, in list order; none for no window.</summary>
    private static IEnumerable<IgnoredApp> Claiming(MappingDocument mapping, WindowIdentity? window, HostPlatform platform, Func<IgnoredApp, bool> kind)
    {
        if (window is null)
        {
            yield break;
        }

        foreach (var app in mapping.Ignored)
        {
            if (app.IsActive && kind(app) && app.Matcher.Matches(window, platform))
            {
                yield return app;
            }
        }
    }

    /// <summary>
    /// Whether the matcher can claim any window here: something is set, and when it names executables, this platform has
    /// names or a known-app guess (a Windows-only list with no guess matches nothing on a Mac, so nothing is watched there).
    /// </summary>
    public static bool CanMatchOn(AppMatcher matcher, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        return !matcher.IsEmptyOn(platform) && (!matcher.HasProcessNames || matcher.EffectiveProcessNames(platform).Count > 0);
    }
}
