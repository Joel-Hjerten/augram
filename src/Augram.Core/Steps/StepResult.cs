namespace Augram.Core.Steps;

/// <summary>What one step reported back to the executor; <see cref="Reason"/> is one line for the log when not <see cref="StepOutcome.Done"/>.</summary>
public sealed record StepResult(StepOutcome Outcome, string? Reason = null)
{
    public static StepResult Done { get; } = new(StepOutcome.Done);

    public static StepResult Skipped(string reason) => new(StepOutcome.Skipped, reason);

    public static StepResult Failed(string reason) => new(StepOutcome.Failed, reason);

    public bool Succeeded => Outcome == StepOutcome.Done;
}
