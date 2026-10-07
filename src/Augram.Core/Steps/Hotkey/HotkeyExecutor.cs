using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// Sends a <see cref="HotkeyStep"/> through <see cref="IInputSimulator.Hotkey"/>: modifiers down (the
/// right-hand key for those in <see cref="HotkeyStep.RightHand"/>, bits outside the modifiers ignored), key
/// tapped, modifiers up in reverse. No key set → Skipped (the command goes on); a simulator answer other
/// than success → Failed with it (the command stops). The settle delay after an activation (A8) is the
/// executor's, applied before this step runs, never here. One Debug line per run.
/// </summary>
internal static class HotkeyExecutor
{
    public const string NoKeyReason = "no key set";

    public static StepResult Execute(HotkeyStep step, StepExecutionContext context)
    {
        var result = Run(step, context.Input);
        context.Log.Debug("steps", "Hotkey", ("keys", step.Summary), ("outcome", result.Outcome), ("reason", result.Reason));
        return result;
    }

    private static StepResult Run(HotkeyStep step, IInputSimulator input)
    {
        if (!step.IsSet)
        {
            return StepResult.Skipped(NoKeyReason);
        }

        var sent = input.Hotkey(step.Modifiers, step.Key, step.RightHand & step.Modifiers);
        return sent == SimulationResult.Success
            ? StepResult.Done
            : StepResult.Failed($"{step.Summary}: {sent}");
    }
}
