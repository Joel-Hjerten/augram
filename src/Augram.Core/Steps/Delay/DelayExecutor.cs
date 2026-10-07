namespace Augram.Core.Steps.Delay;

/// <summary>
/// Blocks the executor thread for the step's duration, waking early when the command is cancelled
/// (Skipped "cancelled", so the executor can stop the chain). Zero milliseconds returns Done without
/// touching the wait handle. Blocking is fine here: the executor thread exists to run steps in order,
/// and nothing on the hook or UI thread waits on it.
/// </summary>
internal static class DelayExecutor
{
    public static StepResult Execute(DelayStep step, StepExecutionContext context)
    {
        if (step.Milliseconds <= 0)
        {
            return StepResult.Done;
        }

        var cancelled = context.Cancellation.WaitHandle.WaitOne(step.Milliseconds);
        return cancelled ? StepResult.Skipped("cancelled") : StepResult.Done;
    }
}
