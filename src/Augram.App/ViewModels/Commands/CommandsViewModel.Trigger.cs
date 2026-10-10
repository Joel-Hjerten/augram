using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The trigger half of <see cref="CommandsViewModel"/> (F1 "Triggers as combinations", Joel 2026-10-09): the kind (Gesture
/// through the Select Gesture picker, Wheel, No trigger), the wheel direction and the "While holding" set. What is edited is
/// this platform's trigger (<see cref="Command.TriggerFor"/>): the original where the command was authored, else its own; the
/// first edit on the other platform makes it that platform's own, the way steps work (<see cref="Command.WithTriggerFor"/>).
/// Changing between gesture and wheel keeps the keys and buttons held; choosing "No trigger" unbinds, and ticking keys or
/// buttons then makes it a click trigger (<see cref="Trigger.WithHold"/>). Every edit goes through <see cref="EditTrigger"/>:
/// it starts from the draft when one waits, is saved as one <c>UpdateCommand</c> when the rules accept it, and otherwise
/// waits as the draft with its note (<c>.TriggerDraft</c>) instead of being refused.
/// </summary>
public sealed partial class CommandsViewModel
{
    private void SetTriggerKind(CommandItem command, TriggerKind kind)
    {
        switch (kind)
        {
            case TriggerKind.None:
                EditTrigger(command.Id, _ => Trigger.None);
                break;
            case TriggerKind.Wheel:
                ChooseWheel(command.Id);
                break;
            case TriggerKind.Gesture:
                _ = PickGestureAsync(command);
                break;
        }
    }

    /// <summary>
    /// Wheel up with the keys and buttons held now (the stroke button alone by default). When the rules refuse it (wheel up is
    /// the volume's here, say) the free direction is saved instead; when both are taken, wheel up waits as the draft with its note.
    /// </summary>
    private void ChooseWheel(CommandId id)
    {
        var current = DraftedTrigger(id);
        if (current is Trigger.WheelTrigger)
        {
            return;
        }

        var up = Trigger.ForWheel(WheelDirection.Up, Held(current));
        if (!TrySaveTrigger(id, up) && !TrySaveTrigger(id, Trigger.ForWheel(WheelDirection.Down, Held(current))))
        {
            KeepDraft(id, up);
        }

        ShowSelected();
    }

    private async Task PickGestureAsync(CommandItem command)
    {
        var current = DraftedTrigger(command.Id) is Trigger.GestureTrigger gesture ? gesture.GestureId : (GestureId?)null;
        var result = await _picker.PickAsync(current).ConfigureAwait(true);
        Guard(() =>
        {
            switch (result.Outcome)
            {
                case GesturePickerOutcome.Selected when result.GestureId is { } id:
                    EditTrigger(command.Id, trigger => Trigger.ForGesture(id, Held(trigger)));
                    break;
                case GesturePickerOutcome.NoGesture:
                    EditTrigger(command.Id, _ => Trigger.None);
                    break;
            }
        });
    }

    /// <summary>
    /// What every trigger edit from the header does: <paramref name="change"/> applies to the draft while one waits, else to the
    /// stored trigger; the result is saved when the rules accept it, else it becomes the draft. The header then shows which.
    /// </summary>
    private void EditTrigger(CommandId id, Func<Trigger, Trigger> change)
    {
        var trigger = change(DraftedTrigger(id));
        if (!TrySaveTrigger(id, trigger))
        {
            KeepDraft(id, trigger);
        }

        ShowSelected();
    }

    /// <summary>
    /// The keys and buttons kept when the kind changes between gesture, wheel and click: all of them, except that a wheel
    /// trigger held without the stroke button keeps only its keys, since a gesture draws with the stroke button.
    /// </summary>
    private static TriggerHold Held(Trigger trigger)
        => !trigger.IsBound ? TriggerHold.Default
            : trigger.Hold.HoldsStroke ? trigger.Hold
            : TriggerHold.WithStroke(trigger.Hold.Keys, capture: trigger.Hold.Capture);

    /// <summary>One store call (one undo step): this platform's trigger is <paramref name="trigger"/>; says so when that made it this platform's own.</summary>
    private void SaveTrigger(CommandId id, Trigger trigger)
    {
        var (group, command) = RequireCommand(id);
        var forked = ForksHere(command);
        _store.UpdateCommand(group.Id, WithTriggerHere(command, trigger));
        if (forked)
        {
            Message = $"'{command.Name}' now has its own trigger and steps here; the original keeps running where it was authored. {CommandsKeymap.Current.Undo} undoes it.";
        }
    }

    /// <summary>True when a trigger edit here makes the command this platform's own (F8: authored on the other platform, no own trigger here yet).</summary>
    private bool ForksHere(Command command)
        => command.Origin is { } origin && origin != _platform && !command.HasOwnTriggerOn(_platform);
}
