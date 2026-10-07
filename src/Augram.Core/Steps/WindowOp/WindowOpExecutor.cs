using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.WindowOp;

/// <summary>
/// Runs a <see cref="WindowOpStep"/> through <see cref="IWindowOperations"/>. No target → skipped;
/// the platform has no equivalent → skipped with the adapter's reason (the command goes on, the UI
/// shows the decline); the adapter failed → failed (the command stops). Window operations never wait
/// (A8); one Debug line per execution for the Diagnostics tab.
/// </summary>
internal static class WindowOpExecutor
{
    public static StepResult Execute(WindowOpStep step, StepExecutionContext context)
    {
        var result = Run(step, context);
        context.Log.Debug(
            "steps",
            "Window operation",
            ("operation", step.Operation),
            ("outcome", result.Outcome),
            ("reason", result.Reason),
            ("process", context.Target?.ProcessName));
        return result;
    }

    private static StepResult Run(WindowOpStep step, StepExecutionContext context)
    {
        if (context.Target is null)
        {
            return StepResult.Skipped("no window under the gesture start");
        }

        var windows = context.Windows;
        if (!windows.Supports(step.Operation))
        {
            return StepResult.Skipped(WindowOperationResult.NotSupported(step.Operation, windows.Platform).Reason!);
        }

        if (step.Operation == WindowOperation.SetSize && step.Size is null)
        {
            return StepResult.Failed("Set size needs a width and a height");
        }

        var performed = windows.Perform(step.Operation, context.Target, step.Size);
        return performed.Succeeded
            ? StepResult.Done
            : StepResult.Failed(performed.Reason ?? $"{step.Operation} failed");
    }
}
