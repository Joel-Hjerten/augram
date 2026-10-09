namespace Augram.Core.Abstractions;

/// <summary>
/// Brings a running app to the front (the Open app step, Joel 2026-10-09): its topmost window, restored when minimized.
/// Platform.Windows finds it among the top-level windows by executable file name; on macOS <see cref="NullAppActivator"/>
/// answers "not running" and the step launches instead, because <c>open -a</c> brings a running app forward by itself.
/// Called on the command executor thread only. Nothing here throws for an expected failure.
/// </summary>
public interface IAppActivator
{
    AppActivation BringToFront(string executable);
}

public enum AppActivationOutcome
{
    /// <summary>A window of the app is in front now.</summary>
    Activated,

    /// <summary>No window of the app is open (or this platform leaves it to the launcher): start it.</summary>
    NotRunning,

    /// <summary>A window was found but the system refused to bring it forward.</summary>
    Failed,
}

public sealed record AppActivation(AppActivationOutcome Outcome, string? Reason = null)
{
    public static AppActivation Activated { get; } = new(AppActivationOutcome.Activated);

    public static AppActivation NotRunning { get; } = new(AppActivationOutcome.NotRunning);

    public static AppActivation Failed(string reason) => new(AppActivationOutcome.Failed, reason);
}

/// <summary>Leaves every app to the launcher: macOS (<c>open -a</c> activates a running app) and tests.</summary>
public sealed class NullAppActivator : IAppActivator
{
    public static NullAppActivator Instance { get; } = new();

    public AppActivation BringToFront(string executable) => AppActivation.NotRunning;
}
