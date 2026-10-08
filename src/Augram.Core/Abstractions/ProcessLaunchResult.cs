namespace Augram.Core.Abstractions;

/// <summary>
/// Outcome of <see cref="IProcessLauncher.Launch"/> with a <see cref="Reason"/> fit for the log: why it failed
/// ("explorer2 was not found"), why it was cancelled ("the administrator prompt was declined"), why it is not supported
/// ("running as administrator is not supported on macOS"), or a note on a start still in progress. Names the file, never
/// the arguments (they may hold something private).
/// </summary>
public sealed record ProcessLaunchResult(ProcessLaunchOutcome Outcome, string? Reason = null)
{
    public static ProcessLaunchResult Started { get; } = new(ProcessLaunchOutcome.Started);

    public static ProcessLaunchResult Failed(string reason) => new(ProcessLaunchOutcome.Failed, reason);

    public static ProcessLaunchResult Cancelled(string reason) => new(ProcessLaunchOutcome.Cancelled, reason);

    public static ProcessLaunchResult NotSupported(string reason) => new(ProcessLaunchOutcome.NotSupported, reason);
}
