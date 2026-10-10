using Augram.Core.Diagnostics;

namespace Augram.App.Hosting;

/// <summary>
/// Whether a launch opens the main window or starts in the tray (and menu bar) only (F7 start at login, 2026-10-10).
/// Hidden when the launch carries <see cref="HiddenArgument"/> (the Windows Run entry does; anyone can pass it) or, on
/// macOS, when it was a login-item launch (a login item takes no arguments there, so the App detects it from the launch
/// event, <c>Platform.MacOS/Startup/MacLaunchEvent</c>). Everything else starts as usual either way: engine, sync, tray. A
/// later launch by the user still shows the window (the single-instance guard on both platforms, a reopen on macOS).
/// </summary>
public sealed record LaunchVisibility(bool Hidden, string? Reason)
{
    public const string HiddenArgument = "--hidden";
    public const string HiddenArgumentReason = HiddenArgument;
    public const string LoginItemReason = "login item";

    public static LaunchVisibility Shown { get; } = new(false, null);

    public static bool HasHiddenArgument(IEnumerable<string>? args) =>
        args?.Any(arg => string.Equals(arg, HiddenArgument, StringComparison.OrdinalIgnoreCase)) == true;

    /// <param name="args">This launch's arguments.</param>
    /// <param name="loginItemLaunch">macOS: the launch event said "launched as a login item"; always false elsewhere.</param>
    public static LaunchVisibility Decide(IEnumerable<string>? args, bool loginItemLaunch)
    {
        if (HasHiddenArgument(args))
        {
            return new LaunchVisibility(true, HiddenArgumentReason);
        }

        return loginItemLaunch ? new LaunchVisibility(true, LoginItemReason) : Shown;
    }

    /// <summary>
    /// What is known before the main loop runs. Null when the answer waits for macOS's launch event (only a login-item
    /// launch can still turn hidden then); <paramref name="waitsForLaunchEvent"/> is false off macOS.
    /// </summary>
    public static LaunchVisibility? AtStart(IEnumerable<string>? args, bool waitsForLaunchEvent) =>
        HasHiddenArgument(args) || !waitsForLaunchEvent ? Decide(args, loginItemLaunch: false) : null;

    /// <summary>For the <c>App started</c> line: <c>hidden=true reason=--hidden</c>, <c>hidden=false</c>.</summary>
    public LogProperty[] LogProperties => Hidden ? [("hidden", true), ("reason", Reason)] : [("hidden", false)];
}
