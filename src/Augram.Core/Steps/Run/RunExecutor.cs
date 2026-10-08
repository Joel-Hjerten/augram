using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.Run;

/// <summary>
/// Starts a <see cref="RunStep"/> through <see cref="IProcessLauncher"/> and returns at once (the launcher never waits for
/// the program). No program set → Skipped (the command goes on); the executor stopping → Skipped "cancelled", nothing
/// started; the launcher's answer → Done when started, Skipped when the user declined the UAC prompt or the platform
/// cannot do it (elevation on macOS), Failed otherwise (not found, no app for the document: the command stops). No
/// activation or settle delay: a program opens its own window. One Debug line per run naming the file, never the
/// arguments (they may hold something private).
/// </summary>
internal static class RunExecutor
{
    public const string NoProgramReason = "no program set";

    public const string CancelledReason = "cancelled";

    public static StepResult Execute(RunStep step, StepExecutionContext context)
    {
        var launched = Launch(step, context);
        var result = launched is null ? Unlaunched(step) : ToResult(step, launched);
        context.Log.Debug(
            "steps",
            "Run",
            ("file", step.File.Trim()),
            ("elevated", step.Elevated),
            ("hidden", step.Hidden),
            ("outcome", result.Outcome),
            ("reason", result.Reason ?? launched?.Reason));
        return result;
    }

    /// <summary>The launcher's answer, or null when nothing may be started (no program, the executor stopping).</summary>
    private static ProcessLaunchResult? Launch(RunStep step, StepExecutionContext context)
        => step.IsSet && !context.Cancellation.IsCancellationRequested ? context.Processes.Launch(step.ToLaunch()) : null;

    private static StepResult Unlaunched(RunStep step)
        => StepResult.Skipped(step.IsSet ? CancelledReason : NoProgramReason);

    private static StepResult ToResult(RunStep step, ProcessLaunchResult launched) => launched.Outcome switch
    {
        ProcessLaunchOutcome.Started => StepResult.Done,
        ProcessLaunchOutcome.Cancelled => StepResult.Skipped(launched.Reason is { } reason ? $"{CancelledReason}: {reason}" : CancelledReason),
        ProcessLaunchOutcome.NotSupported => StepResult.Skipped(launched.Reason ?? $"{step.Target} is not supported here"),
        _ => StepResult.Failed(launched.Reason ?? $"{step.Target} could not be started"),
    };
}
