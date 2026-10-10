using Augram.Core.Abstractions;

namespace Augram.App.Hosting;

/// <summary>
/// Who wins when Augram's start-at-login setting and the OS disagree (F7, 2026-10-10), for <see cref="AppState"/>. The
/// setting wins, so a missing entry (the config restored on another machine, the executable moved) or an outdated one
/// (another path, or no <c>--hidden</c>) is written again, and "off" removes whatever is there. One exception, at startup
/// only: when the setting is on but the user turned it off outside Augram (Task Manager › Startup apps; System Settings ›
/// General › Login Items), the setting follows the OS instead of switching it back on behind the user's back. Turning it
/// on in Augram afterwards means on: the registration clears the switch-off (Windows) or adds the item again (macOS).
/// </summary>
public static class StartupPolicy
{
    public const string TaskManager = "Task Manager › Startup apps";
    public const string LoginItems = "System Settings › General › Login Items";

    /// <summary>At startup: the saved choice against what the OS reports.</summary>
    public static StartupAction AtStartup(bool wanted, StartupStatus status) => wanted
        ? status switch
        {
            StartupStatus.Registered => StartupAction.None,
            StartupStatus.DisabledByUser or StartupStatus.NeedsApproval => StartupAction.FollowTheSystem,
            _ => StartupAction.Register,
        }
        : Off(status);

    /// <summary>The setting changed in Augram (the toggle, the tray, an undo, or following the OS).</summary>
    public static StartupAction OnChange(bool wanted, StartupStatus status) => wanted
        ? status == StartupStatus.Registered ? StartupAction.None : StartupAction.Register
        : Off(status);

    /// <summary>
    /// The state note under the toggle after the setting followed the OS: "Turned off in Task Manager › Startup apps.";
    /// empty for a status that is not an "off" made outside Augram.
    /// </summary>
    public static string FollowedNote(StartupStatus status, HostPlatform platform) => (status, platform) switch
    {
        (StartupStatus.DisabledByUser, HostPlatform.Windows) => $"Turned off in {TaskManager}.",
        (StartupStatus.DisabledByUser, HostPlatform.MacOS) => $"Removed in {LoginItems}.",
        (StartupStatus.NeedsApproval, _) => $"Not allowed in {LoginItems}.",
        _ => string.Empty,
    };

    /// <summary>The state note while the setting is on: only macOS's pending approval has one ("Waiting for approval in …").</summary>
    public static string StatusNote(bool wanted, StartupStatus status) =>
        wanted && status == StartupStatus.NeedsApproval ? $"Waiting for approval in {LoginItems}." : string.Empty;

    private static StartupAction Off(StartupStatus status) =>
        status == StartupStatus.NotRegistered ? StartupAction.None : StartupAction.Unregister;
}
