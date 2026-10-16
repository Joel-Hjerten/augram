using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// An app Augram stays out of (F5's ignore list). A <see cref="IgnoreScope.Global"/> entry (Ignored › Global) works in one of
/// two modes: with <see cref="DisableEntirely"/> false, gestures do not fire over its windows (the stroke button reaches the
/// app); with it true, Augram switches itself off while the app has focus (SP.net's "Disable if this App Gains Focus", used for
/// VMware so the guest gets the raw button). A <see cref="IgnoreScope.PerCommand"/> entry (Ignored › Per command, Joel
/// 2026-10-10, plan 0004) does nothing on its own: it is an app the commands that name it in their "Not in"
/// (<see cref="Command.NotIn"/>) are not used over, and its <see cref="DisableEntirely"/> is always false. A
/// <see cref="IgnoreScope.WhileHolding"/> entry (Exclusions › While holding, plan 0012) makes every press over the app pass
/// through while one of its <see cref="Holding"/> inputs is held; its <see cref="DisableEntirely"/> is always false too.
/// </summary>
public sealed record IgnoredApp(
    GroupId Id,
    string Name,
    bool IsActive,
    AppMatcher Matcher,
    bool DisableEntirely)
{
    /// <summary>Which list it is on: the whole of Augram (Global, the default), only the commands that name it (Per command), or while an input is held (While holding).</summary>
    public IgnoreScope Scope { get; init; } = IgnoreScope.Global;

    public bool IsGlobal => Scope == IgnoreScope.Global;

    public bool IsPerCommand => Scope == IgnoreScope.PerCommand;

    public bool IsWhileHolding => Scope == IgnoreScope.WhileHolding;

    /// <summary>
    /// The "Ignore gestures while holding" list of a While holding entry (plan 0012 decisions 2 and 5): its inputs per platform,
    /// an original and an optional own version for the other platform, by the rules a command's steps follow
    /// (<see cref="PlatformSides"/>). Empty on the other lists (<see cref="MappingRules.Normalised(IgnoredApp)"/>).
    /// </summary>
    public PlatformList<WhileHoldingInput> Holding { get; init; } = PlatformList<WhileHoldingInput>.Empty;

    /// <summary>
    /// The inputs that count on <paramref name="platform"/>: its side of <see cref="Holding"/>, each as it reads there (an
    /// inferred side's converted, one with no counterpart left out), each once, in list order. Empty off the While holding list.
    /// </summary>
    public IReadOnlyList<KeyOrButton> HeldInputsOn(HostPlatform platform)
    {
        if (!IsWhileHolding || Holding.IsEmpty)
        {
            return [];
        }

        var inputs = new List<KeyOrButton>();
        foreach (var row in Holding.For(platform))
        {
            if (row.ForPlatform(platform) is { } input && !inputs.Contains(input))
            {
                inputs.Add(input);
            }
        }

        return inputs;
    }
}

/// <summary>The three lists of the Exclusions tab (plans 0004 and 0012): everything Augram does, only the commands that name the app, or while an input is held.</summary>
public enum IgnoreScope
{
    Global,
    PerCommand,
    WhileHolding,
}
