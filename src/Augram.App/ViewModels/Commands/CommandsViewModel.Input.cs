using Augram.App.Components.CommandTree;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The input half of <see cref="CommandsViewModel"/> (F9, plan 0002 step 4): a command under a hold remap has an input where
/// other commands have a trigger (<see cref="Trigger.InputTrigger"/>), edited in the header's Input rows. Every edit goes
/// through <c>EditTrigger</c>, as a trigger edit does: saved as one <c>UpdateCommand</c> when the rules accept it, else kept as
/// the draft with the rule's words (<c>.TriggerDraft</c>), so a set being composed (no button yet, no key yet) or an input
/// another command of the hold remap already uses waits in the header instead of snapping back. Choosing Wheel takes wheel
/// up, else the free direction, else waits as wheel up; Buttons and Key start empty, waiting for a button or a key.
/// </summary>
public sealed partial class CommandsViewModel
{
    private void SetInputKind(CommandItem command, InputKind kind)
    {
        if (InputKindExtensions.KindOf(DraftedTrigger(command.Id)) == kind)
        {
            return;
        }

        switch (kind)
        {
            case InputKind.Buttons:
                EditTrigger(command.Id, _ => Trigger.ForInput(new HoldInput.Buttons(HeldButtons.None)));
                break;
            case InputKind.Key:
                EditTrigger(command.Id, _ => Trigger.ForInput(new HoldInput.Key(KeyCode.None)));
                break;
            case InputKind.Wheel:
                ChooseWheelInput(command.Id);
                break;
            default:
                EditTrigger(command.Id, _ => Trigger.None);
                break;
        }
    }

    private void SetInput(CommandItem command, HoldInput input) => EditTrigger(command.Id, _ => Trigger.ForInput(input));

    /// <summary>
    /// The command with <paramref name="trigger"/> as this platform's trigger, one edit: for an input, its Remap step's output is
    /// first fitted to the input (<see cref="HoldRemapRules.FittedTo"/>: a button output on a wheel input becomes a notch the
    /// same way, a wheel output elsewhere Middle), so changing the input is never refused for the output, and the undo of it
    /// brings both back. Every trigger save and the draft's note go through this.
    /// </summary>
    private Command WithTriggerHere(Command command, Trigger trigger)
    {
        var now = DateTimeOffset.UtcNow;
        if (trigger is Trigger.InputTrigger { Input: var input })
        {
            var steps = EditableSteps(command);
            var fitted = steps.Select(step => step.Step is RemapStep remap ? step with { Step = new RemapStep(HoldRemapRules.FittedTo(remap.Output, input)) } : step).ToList();
            if (!fitted.SequenceEqual(steps))
            {
                command = command.WithStepsFor(_platform, fitted, now);
            }
        }

        return command.WithTriggerFor(_platform, trigger, now);
    }

    /// <summary>A new step of <paramref name="type"/> for the command: the type's default, a Remap step's output fitted to the command's input here (a wheel input starts with a wheel notch the same way), so it is never refused for its output.</summary>
    private IStep NewStep(IStepType type, Command command)
    {
        var step = type.CreateDefault();
        return step is RemapStep remap && command.TriggerFor(_platform) is Trigger.InputTrigger { Input: var input }
            ? new RemapStep(HoldRemapRules.FittedTo(remap.Output, input))
            : step;
    }

    private void ChooseWheelInput(CommandId id)
    {
        var up = Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Up));
        if (!TrySaveTrigger(id, up) && !TrySaveTrigger(id, Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Down))))
        {
            KeepDraft(id, up);
        }

        ShowSelected();
    }
}
