using Augram.App.Components.CommandTree;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

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
