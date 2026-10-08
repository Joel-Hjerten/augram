using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.Hdr;

/// <summary>
/// Runs an <see cref="HdrStep"/> through <see cref="IDisplayModes"/>. The platform cannot switch HDR, no display, or the
/// display has no HDR → skipped with the reason (the command goes on); already in the wanted state → done without a
/// change; the adapter failed → failed. Toggle reads the state fresh, so two quick toggles land where they should.
/// Command executor thread only; one Debug line per execution.
/// </summary>
internal static class HdrExecutor
{
    public static StepResult Execute(HdrStep step, StepExecutionContext context)
    {
        DisplayInfo? display = null;
        var result = Run(step, context, ref display);
        context.Log.Debug(
            "steps",
            "HDR",
            ("action", step.Action),
            ("display", display?.Name),
            ("was", display?.Hdr),
            ("outcome", result.Outcome),
            ("reason", result.Reason));
        return result;
    }

    private static StepResult Run(HdrStep step, StepExecutionContext context, ref DisplayInfo? display)
    {
        var displays = context.Displays;
        if (!displays.CanSwitchHdr)
        {
            return StepResult.Skipped($"switching HDR is not supported on {displays.Platform}");
        }

        display = DisplayLookup.Find(displays.Displays(), step.Target, context.Start.X, context.Start.Y);
        if (display is null)
        {
            return StepResult.Skipped(step.Target == DisplayTarget.Main ? "no main display reported" : "no display under the gesture start");
        }

        if (display.Hdr == HdrState.Unsupported)
        {
            return StepResult.Skipped($"{display.Name} does not support HDR");
        }

        var on = step.Action switch
        {
            HdrAction.On => true,
            HdrAction.Off => false,
            _ => display.Hdr != HdrState.On,
        };
        if (on == (display.Hdr == HdrState.On))
        {
            return StepResult.Done;
        }

        var switched = displays.SetHdr(display, on);
        return switched.Succeeded
            ? StepResult.Done
            : StepResult.Failed(switched.Reason ?? $"{display.Name} did not switch HDR {(on ? "on" : "off")}");
    }
}
