using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Steps.Remap;

/// <summary>
/// The "Remap" step type: key <c>remap</c>, category Mouse, offered only for commands under a hold remap
/// (<see cref="HoldRemapsOnly"/>), the same on both platforms (F9: Blender's Ctrl + Middle zoom is Ctrl on the Mac too; a
/// platform version gives another output where one is needed). Parameters, one of:
/// <c>{ "output": "Button", "button": "Middle", "modifiers": "Control" }</c>,
/// <c>{ "output": "Key", "key": "R", "modifiers": "Control, Alt, Shift", "rightHand": "Alt" }</c>,
/// <c>{ "output": "Wheel", "direction": "Up", "modifiers": "Control" }</c>; names as the enums spell them, case-insensitive,
/// <c>modifiers</c> and <c>rightHand</c> written only when there are any (read by the Hotkey step's reader). Absent members
/// default: a button output, Middle, no key, Up, no modifiers. The default instance is Middle with nothing held: Joel's
/// orbit. The command executor never runs it (<see cref="Execute"/> skips); the engine worker plays the output.
/// </summary>
public sealed class RemapStepType : IStepType
{
    public const string OutputMember = "output";

    public const string ButtonMember = "button";

    public const string KeyMember = "key";

    public const string DirectionMember = "direction";

    public const string ModifiersMember = "modifiers";

    public const string RightHandMember = "rightHand";

    /// <summary>Why the command executor does not run it; the step result and the log line say this.</summary>
    public const string NotRunHere = "a Remap step is played by its hold remap while the hold key is held, never as a command step";

    private RemapStepType()
    {
    }

    public static RemapStepType Instance { get; } = new();

    public string Key => "remap";

    public string DisplayName => "Remap";

    public StepCategory Category => StepCategory.Mouse;

    public bool IsPlatformNeutral => true;

    /// <summary>Plan 0002: the picker offers it for a command under a hold remap and nowhere else.</summary>
    public bool HoldRemapsOnly => true;

    /// <summary>The same on both platforms (F9): outputs are never converted.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to)
        => StepConversion.Same(StepParameters.Expect<RemapStep>(step, this));

    public IStep CreateDefault() => new RemapStep(new RemapOutput.Button(MouseButton.Middle));

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var modifiers = HotkeyStepType.ReadModifiers(parameters, ModifiersMember) & HotkeyKeys.AllModifiers;
        RemapOutput output = (StepParameters.ReadEnum<RemapOutputKind>(parameters, OutputMember) ?? RemapOutputKind.Button) switch
        {
            RemapOutputKind.Key => new RemapOutput.Key(
                StepParameters.ReadEnum<KeyCode>(parameters, KeyMember) ?? KeyCode.None,
                modifiers,
                HotkeyStepType.ReadModifiers(parameters, RightHandMember) & modifiers),
            RemapOutputKind.Wheel => new RemapOutput.Wheel(StepParameters.ReadEnum<ScrollDirection>(parameters, DirectionMember) ?? ScrollDirection.Up, modifiers),
            _ => new RemapOutput.Button(StepParameters.ReadEnum<MouseButton>(parameters, ButtonMember) ?? MouseButton.Middle, modifiers),
        };
        return new RemapStep(output);
    }

    public JsonObject Write(IStep step)
    {
        var output = StepParameters.Expect<RemapStep>(step, this).Output;
        var parameters = new JsonObject { [OutputMember] = output.Kind.ToString() };
        var modifiers = output.Modifiers & HotkeyKeys.AllModifiers;
        var rightHand = KeyModifiers.None;
        switch (output)
        {
            case RemapOutput.Button button:
                parameters[ButtonMember] = button.MouseButton.ToString();
                break;
            case RemapOutput.Key key:
                parameters[KeyMember] = key.KeyCode.ToString();
                rightHand = key.RightHand & modifiers;
                break;
            case RemapOutput.Wheel wheel:
                parameters[DirectionMember] = wheel.Direction.ToString();
                break;
        }

        if (modifiers != KeyModifiers.None)
        {
            parameters[ModifiersMember] = modifiers.ToString();
        }

        if (rightHand != KeyModifiers.None)
        {
            parameters[RightHandMember] = rightHand.ToString();
        }

        return parameters;
    }

    /// <summary>Never runs here: skipped with <see cref="NotRunHere"/>, one Debug line.</summary>
    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var remap = StepParameters.Expect<RemapStep>(step, this);
        context.Log.Debug("steps", "Remap", ("output", remap.Output.Describe(HotkeyText.Names)), ("outcome", StepOutcome.Skipped), ("reason", NotRunHere));
        return StepResult.Skipped(NotRunHere);
    }
}
