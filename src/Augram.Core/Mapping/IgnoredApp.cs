namespace Augram.Core.Mapping;

/// <summary>
/// An app Augram stays out of (F5's ignore list). A <see cref="IgnoreScope.Global"/> entry (Ignored › Global) works in one of
/// two modes: with <see cref="DisableEntirely"/> false, gestures do not fire over its windows (the stroke button reaches the
/// app); with it true, Augram switches itself off while the app has focus (SP.net's "Disable if this App Gains Focus", used for
/// VMware so the guest gets the raw button). A <see cref="IgnoreScope.PerCommand"/> entry (Ignored › Per command, Joel
/// 2026-10-10, plan 0004) does nothing on its own: it is an app the commands that name it in their "Not in"
/// (<see cref="Command.NotIn"/>) are not used over, and its <see cref="DisableEntirely"/> is always false.
/// </summary>
public sealed record IgnoredApp(
    GroupId Id,
    string Name,
    bool IsActive,
    AppMatcher Matcher,
    bool DisableEntirely)
{
    /// <summary>Which list it is on: the whole of Augram (Global, the default) or only the commands that name it (Per command).</summary>
    public IgnoreScope Scope { get; init; } = IgnoreScope.Global;

    public bool IsPerCommand => Scope == IgnoreScope.PerCommand;
}

/// <summary>The two lists of the Ignored tab (plan 0004): everything Augram does, or only the commands that name the app.</summary>
public enum IgnoreScope
{
    Global,
    PerCommand,
}
