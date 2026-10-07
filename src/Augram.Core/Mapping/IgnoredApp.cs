namespace Augram.Core.Mapping;

/// <summary>
/// An app Augram stays out of (F5's ignore list), in one of two modes: with <see cref="DisableEntirely"/>
/// false, gestures do not fire over its windows (the stroke button reaches the app); with it true,
/// Augram switches itself off while the app has focus (SP.net's "Disable if this App Gains Focus",
/// used for VMware so the guest gets the raw button).
/// </summary>
public sealed record IgnoredApp(
    GroupId Id,
    string Name,
    bool IsActive,
    AppMatcher Matcher,
    bool DisableEntirely);
