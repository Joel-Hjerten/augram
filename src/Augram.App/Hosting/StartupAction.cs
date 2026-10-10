namespace Augram.App.Hosting;

/// <summary>What <see cref="StartupPolicy"/> tells <see cref="AppState"/> to do about the OS registration.</summary>
public enum StartupAction
{
    None,

    /// <summary>Register, or rewrite an outdated entry, or undo a switch-off made outside Augram.</summary>
    Register,

    /// <summary>Remove the entry, whatever state it is in.</summary>
    Unregister,

    /// <summary>Startup only: the user turned it off outside Augram; turn the setting off instead of the OS on.</summary>
    FollowTheSystem,
}
