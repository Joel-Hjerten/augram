using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.ClearClipboard;

/// <summary>
/// Empties the clipboard through <see cref="IClipboard"/> on the executor thread. Cleared → Done; no clipboard adapter on
/// this platform → Skipped with the reason (the command goes on); the adapter could not do it (another app holds the
/// clipboard past the retries) → Failed with its reason (the command stops). No activation or settle delay: the
/// clipboard is the system's, not the window's. One Debug line per run.
/// </summary>
internal static class ClearClipboardExecutor
{
    public static StepResult Execute(ClearClipboardStep step, StepExecutionContext context)
    {
        var cleared = context.Clipboard.Clear();
        var result = cleared.Succeeded ? StepResult.Done
            : cleared.IsSupported ? StepResult.Failed(cleared.Reason ?? "the clipboard could not be cleared")
            : StepResult.Skipped(cleared.Reason ?? NullClipboard.Reason);
        context.Log.Debug("steps", step.Summary, ("outcome", result.Outcome), ("reason", result.Reason));
        return result;
    }
}
