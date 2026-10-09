using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.DisplayMode;

/// <summary>
/// Runs a <see cref="DisplayModeStep"/> through <see cref="IDisplayModes"/>: reads the displays fresh, finds the target
/// (under the gesture start or the main one, <see cref="DisplayLookup"/>), resolves the whole mode
/// (<see cref="DisplayModeResolver"/>) and only then applies it, in one call. Nothing to change, no display, or a mode
/// the display lacks → skipped with the reason (the command goes on); the mode already current → done without a
/// change; the adapter failed → failed (the command stops). Runs on the command executor thread, holds no lock, and
/// may block for the second or two a display takes to re-sync. One Debug line per execution.
/// </summary>
internal static class DisplayModeExecutor
{
    public static StepResult Execute(DisplayModeStep step, StepExecutionContext context)
    {
        DisplayInfo? display = null;
        DisplayModeResolution? resolved = null;
        var result = Run(step, context, ref display, ref resolved);
        context.Log.Debug(
            "steps",
            "Display mode",
            ("requested", step.Summary),
            ("display", display?.Name),
            ("from", display?.Current.ToString()),
            ("to", resolved?.Mode?.ToString()),
            ("outcome", result.Outcome),
            ("reason", result.Reason ?? (resolved?.IsCurrent == true ? "already current" : null)));
        return result;
    }

    private static StepResult Run(DisplayModeStep step, StepExecutionContext context, ref DisplayInfo? display, ref DisplayModeResolution? resolved)
    {
        if (step.ChangesNothing)
        {
            return StepResult.Skipped("nothing to change: resolution and refresh are both Auto");
        }

        var displays = context.Displays.Displays();
        if (displays.Count == 0)
        {
            return StepResult.Skipped($"no displays reported on {context.Displays.Platform}");
        }

        display = DisplayLookup.Find(displays, step.Target, context.Start.X, context.Start.Y);
        if (display is null)
        {
            return StepResult.Skipped("no display under the gesture start");
        }

        resolved = DisplayModeResolver.Resolve(display, step.Resolution, step.Refresh, step.HighestRefresh);
        if (resolved.Mode is not { } mode)
        {
            return StepResult.Skipped(resolved.Reason ?? $"{display.Name} cannot run {step.Summary}");
        }

        if (resolved.IsCurrent)
        {
            return StepResult.Done;
        }

        var applied = context.Displays.SetMode(display, mode);
        return applied.Succeeded
            ? StepResult.Done
            : StepResult.Failed(applied.Reason ?? $"{display.Name} did not change to {mode}");
    }
}
