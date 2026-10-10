using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// The command half of the hold remap rules (plan 0002, Core table), run by <see cref="MappingRules"/> for every command of a
/// normalised group, on both platforms (the original and an own version): an input only under a hold remap and only an input
/// there (or no trigger yet); a button input names at least one button; a key input is a key, not a modifier (they pass
/// through a hold, decision 4) and not the hold key; a Remap step is a command's only step; a wheel input takes a key or a
/// wheel output, and a wheel output needs a wheel input. Inputs unique per hold remap is A7, in
/// <see cref="MappingRules.Overlap"/>.
/// </summary>
public static partial class HoldRemapRules
{
    private static readonly HostPlatform[] Platforms = [HostPlatform.Windows, HostPlatform.MacOS];

    /// <summary>Checks a normalised command of <paramref name="group"/> against the rules above.</summary>
    public static void EnsureValid(Command command, AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(group);
        var holdRemap = group.HoldRemapOf(command);
        foreach (var platform in Platforms)
        {
            var trigger = command.TriggerFor(platform);
            EnsureTrigger(command, holdRemap, trigger);
            EnsureSteps(command, trigger, command.StepsFor(platform));
        }
    }

    private static void EnsureTrigger(Command command, HoldRemap? holdRemap, Trigger trigger)
    {
        if (trigger is not Trigger.InputTrigger { Input: var input })
        {
            if (holdRemap is not null && trigger.IsBound)
            {
                throw new MappingValidationException(
                    $"'{command.Name}' is under the hold remap '{holdRemap.Name}': its trigger must be an input (a button, buttons held together, a wheel direction or a key), not {trigger.Describe()}.");
            }

            return;
        }

        if (holdRemap is null)
        {
            throw new MappingValidationException($"'{command.Name}' has the input {input.Describe()} but is not under a hold remap: only a command under one has an input.");
        }

        switch (input)
        {
            case HoldInput.Buttons { Set: var set } when (set & HeldButtonsExtensions.Physical) == HeldButtons.None:
                throw new MappingValidationException($"'{command.Name}' needs at least one button as its input.");
            case HoldInput.Key { KeyCode: KeyCode.None }:
                throw new MappingValidationException($"'{command.Name}' needs a key as its input.");
            case HoldInput.Key { KeyCode: var key } when InputKeyReason(key, holdRemap) is { } reason:
                throw new MappingValidationException($"'{command.Name}' cannot use {HotkeyText.KeyName(key)} as its input: {reason}.");
        }
    }

    /// <summary>
    /// Why <paramref name="key"/> cannot be the input of a command under <paramref name="holdRemap"/> ("Left Shift cannot be an
    /// input of 'Space': Ctrl, Alt, Shift and Win pass through while a hold key is held."); null when it can. The same reason
    /// <see cref="EnsureValid(Command, AppGroup)"/> refuses such a command with; the App's input key field asks it before it
    /// takes a key. The keys are named as <see cref="HotkeyText.Names"/>' platform names them ("Ctrl, Opt, Shift and Cmd" on macOS).
    /// </summary>
    public static string? InputKeyProblem(KeyCode key, HoldRemap holdRemap) => InputKeyProblem(key, holdRemap, HotkeyText.Names);

    /// <summary><see cref="InputKeyProblem(KeyCode, HoldRemap)"/> with the keys named as <paramref name="names"/> names them.</summary>
    public static string? InputKeyProblem(KeyCode key, HoldRemap holdRemap, HostPlatform names)
    {
        ArgumentNullException.ThrowIfNull(holdRemap);
        return InputKeyReason(key, holdRemap, names) is { } reason ? $"{HotkeyText.KeyName(key, names)} cannot be an input of '{holdRemap.Name}': {reason}." : null;
    }

    /// <summary>
    /// <paramref name="output"/> as a command with <paramref name="input"/> may have it: rule 7 read the other way. A button
    /// output on a wheel input becomes a wheel notch the same way as the input, a wheel output on any other input the Middle
    /// button (the Remap step's default); the modifiers stay; an output the input already takes, or no input yet, is returned as
    /// it is. The editor applies it to a new Remap step and when a command's input changes, so neither is refused for its output.
    /// </summary>
    public static RemapOutput FittedTo(RemapOutput output, HoldInput? input)
    {
        ArgumentNullException.ThrowIfNull(output);
        return (input, output) switch
        {
            (HoldInput.Wheel wheel, RemapOutput.Button button) => new RemapOutput.Wheel(wheel.Direction == WheelDirection.Up ? ScrollDirection.Up : ScrollDirection.Down, button.Modifiers),
            (HoldInput.Buttons or HoldInput.Key, RemapOutput.Wheel notch) => new RemapOutput.Button(MouseButton.Middle, notch.Modifiers),
            _ => output,
        };
    }

    private static string? InputKeyReason(KeyCode key, HoldRemap holdRemap, HostPlatform? names = null)
        => HotkeyKeys.IsModifier(key) ? $"{HotkeyText.ModifierList("and", names)} pass through while a hold key is held"
            : key != KeyCode.None && key == holdRemap.HoldKey ? $"it is the hold key of '{holdRemap.Name}'"
            : null;

    private static void EnsureSteps(Command command, Trigger trigger, IReadOnlyList<CommandStep> steps)
    {
        var remap = steps.FirstOrDefault(step => step.Step is RemapStep)?.Step as RemapStep;
        if (remap is null)
        {
            return;
        }

        if (steps.Count > 1)
        {
            throw new MappingValidationException($"'{command.Name}' has a Remap step among other steps: a Remap step is a command's only step.");
        }

        if (trigger is not Trigger.InputTrigger { Input: var input })
        {
            return;
        }

        if (input is HoldInput.Wheel && remap.Output is RemapOutput.Button)
        {
            throw new MappingValidationException($"'{command.Name}' turns the wheel: its Remap output must be a key or a wheel notch, not a button.");
        }

        if (input is not HoldInput.Wheel && remap.Output is RemapOutput.Wheel)
        {
            throw new MappingValidationException($"'{command.Name}' sends a wheel notch: that output is for a wheel input only.");
        }
    }
}
