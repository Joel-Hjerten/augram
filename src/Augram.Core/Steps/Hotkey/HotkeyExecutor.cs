using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// Sends a <see cref="HotkeyStep"/> through <see cref="IInputSimulator.Hotkey"/>: modifiers down (the
/// right-hand key for those in <see cref="HotkeyBinding.RightHand"/>, bits outside the modifiers ignored), key
/// tapped, modifiers up in reverse. No key set → Skipped (the command goes on); a simulator answer other
/// than success → Failed with it (the command stops). A step with layout shortcuts (plan 0011) asks
/// <see cref="StepExecutionContext.Layouts"/> which layout is active, once, and presses that layout's shortcut when it
/// has one, else the main one; a step without never asks. The settle delay after an activation (A8) is the
/// executor's, applied before this step runs, never here. One Debug line per run; with layout shortcuts it also names the
/// layout and whether the "layout shortcut" or the "fallback" ran.
/// </summary>
internal static class HotkeyExecutor
{
    public const string NoKeyReason = "no key set";

    public const string LayoutShortcut = "layout shortcut";

    public const string Fallback = "fallback";

    public const string UnknownLayout = "unknown";

    public static StepResult Execute(HotkeyStep step, StepExecutionContext context)
    {
        if (!step.HasLayouts)
        {
            var plain = Run(step.Main, context.Input);
            context.Log.Debug(LogSources.Steps, "Hotkey", ("keys", step.Summary), ("outcome", plain.Outcome), ("reason", plain.Reason));
            return plain;
        }

        var layout = context.Layouts.Active;
        bool own = layout is not null && step.Layouts.ContainsKey(layout.Id);
        var binding = step.ForLayout(layout?.Id);
        var result = Run(binding, context.Input);
        context.Log.Debug(
            LogSources.Steps,
            "Hotkey",
            ("keys", binding.Summary),
            ("outcome", result.Outcome),
            ("reason", result.Reason),
            ("layout", layout?.Name ?? UnknownLayout),
            ("shortcut", own ? LayoutShortcut : Fallback));
        return result;
    }

    private static StepResult Run(HotkeyBinding binding, IInputSimulator input)
    {
        if (!binding.IsSet)
        {
            return StepResult.Skipped(NoKeyReason);
        }

        var sent = input.Hotkey(binding.Modifiers, binding.Key, binding.RightHand & binding.Modifiers);
        return sent == SimulationResult.Success
            ? StepResult.Done
            : StepResult.Failed($"{binding.Summary}: {sent}");
    }
}
