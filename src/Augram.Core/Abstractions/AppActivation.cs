namespace Augram.Core.Abstractions;

/// <summary><see cref="IAppActivator.BringToFront"/>'s answer, with the reason when it failed.</summary>
public sealed record AppActivation(AppActivationOutcome Outcome, string? Reason = null)
{
    public static AppActivation Activated { get; } = new(AppActivationOutcome.Activated);

    public static AppActivation NotRunning { get; } = new(AppActivationOutcome.NotRunning);

    public static AppActivation Failed(string reason) => new(AppActivationOutcome.Failed, reason);
}
