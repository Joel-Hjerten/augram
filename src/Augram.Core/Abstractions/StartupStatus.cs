namespace Augram.Core.Abstractions;

/// <summary>
/// Start at login as the OS reports it (<see cref="IStartupRegistration.Status"/>). The App compares it with the saved
/// setting: the setting wins, except over an "off" the user made outside Augram (<see cref="DisabledByUser"/>,
/// <see cref="NeedsApproval"/>), which the setting follows at startup.
/// </summary>
public enum StartupStatus
{
    /// <summary>No entry: never registered on this machine, or removed by Augram.</summary>
    NotRegistered,

    /// <summary>Registered, and starts this Augram at login.</summary>
    Registered,

    /// <summary>An entry that starts something else: another executable path, or a command without the hidden-launch argument. Registering again rewrites it.</summary>
    Outdated,

    /// <summary>
    /// The user turned it off outside Augram: Task Manager › Startup apps on Windows; on macOS, Augram removed from System
    /// Settings › General › Login Items.
    /// </summary>
    DisabledByUser,

    /// <summary>macOS: registered, but the user has to allow it in System Settings › General › Login Items before it starts anything.</summary>
    NeedsApproval,
}
