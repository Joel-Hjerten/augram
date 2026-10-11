namespace Augram.Core.Abstractions;

/// <summary>What <see cref="IAppActivator.BringToFront"/> did.</summary>
public enum AppActivationOutcome
{
    /// <summary>A window of the app is in front now.</summary>
    Activated,

    /// <summary>No window of the app is open (or this platform leaves it to the launcher): start it.</summary>
    NotRunning,

    /// <summary>A window was found but the system refused to bring it forward.</summary>
    Failed,
}
