using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.MediaKey;

/// <summary>
/// Presses and releases the step's key through <see cref="IInputSimulator"/>. A press that does not
/// succeed is Failed with the simulator's answer and no release is sent (nothing is down); a release
/// that does not succeed is Failed too, so the log shows which half went wrong. One Debug line per run.
/// </summary>
internal static class MediaKeyExecutor
{
    public static StepResult Execute(MediaKeyStep step, StepExecutionContext context)
    {
        var code = step.Key.ToKeyCode();
        var result = Run(code, context.Input);
        context.Log.Debug("steps", "Media key", ("key", step.Key), ("code", code), ("outcome", result.Outcome), ("reason", result.Reason));
        return result;
    }

    private static StepResult Run(KeyCode code, IInputSimulator input)
    {
        var pressed = input.KeyPress(code);
        if (pressed != SimulationResult.Success)
        {
            return StepResult.Failed($"{code} press: {pressed}");
        }

        var released = input.KeyRelease(code);
        return released == SimulationResult.Success
            ? StepResult.Done
            : StepResult.Failed($"{code} release: {released}");
    }
}
