using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Steps.Scroll;

/// <summary>
/// Runs a <see cref="ScrollStep"/> through <see cref="IInputSimulator"/>: the held keys down (left-hand keys, Ctrl, Alt,
/// Shift, Meta order), <see cref="IInputSimulator.Scroll"/> at the gesture start, the keys up in reverse. Every key that
/// went down comes up, in a <c>finally</c>, whatever happened between (A19); a key the user holds is never released by
/// this step unless it pressed it too. A press, the scroll or a release that does not succeed is Failed with the
/// simulator's answer (no scroll after a failed press). The activation and settle delay (A8) are the executor's, applied
/// before this step because it is a Mouse step. One Debug line per run.
/// </summary>
internal static class ScrollExecutor
{
    public static StepResult Execute(ScrollStep step, StepExecutionContext context)
    {
        var result = Run(step, context);
        context.Log.Debug(
            "steps",
            "Scroll",
            ("direction", step.Direction),
            ("notches", step.NotchCount),
            ("keys", step.HeldKeys),
            ("outcome", result.Outcome),
            ("reason", result.Reason));
        return result;
    }

    private static StepResult Run(ScrollStep step, StepExecutionContext context)
    {
        var input = context.Input;
        var pressed = new List<KeyCode>(4);
        StepResult result;
        StepResult? released;
        try
        {
            result = PressAndScroll(step, context, pressed);
        }
        finally
        {
            released = Release(input, pressed);
        }

        return result.Outcome == StepOutcome.Done && released is { } failure ? failure : result;
    }

    private static StepResult PressAndScroll(ScrollStep step, StepExecutionContext context, List<KeyCode> pressed)
    {
        var input = context.Input;
        foreach (var key in HotkeyKeys.LeftKeys(step.HeldKeys))
        {
            var down = input.KeyPress(key);
            if (down != SimulationResult.Success)
            {
                return StepResult.Failed($"{key} press: {down}");
            }

            pressed.Add(key);
        }

        var scrolled = input.Scroll(step.Direction, step.NotchCount, context.Start.X, context.Start.Y);
        return scrolled == SimulationResult.Success
            ? StepResult.Done
            : StepResult.Failed($"{step.Summary}: {scrolled}");
    }

    /// <summary>Releases <paramref name="pressed"/> in reverse order, every one even after a failure; the first failure, or null.</summary>
    private static StepResult? Release(IInputSimulator input, List<KeyCode> pressed)
    {
        StepResult? failure = null;
        for (var index = pressed.Count - 1; index >= 0; index--)
        {
            var up = input.KeyRelease(pressed[index]);
            if (up != SimulationResult.Success && failure is null)
            {
                failure = StepResult.Failed($"{pressed[index]} release: {up}");
            }
        }

        return failure;
    }
}
