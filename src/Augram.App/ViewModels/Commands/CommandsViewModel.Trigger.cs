using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The trigger half of <see cref="CommandsViewModel"/> (F1 "Triggers as combinations", Joel 2026-10-09): the kind (Gesture
/// through the Select Gesture picker, Wheel, No trigger), the wheel direction and the "While holding" set, each one
/// <c>UpdateCommand</c>. What is edited is this platform's trigger (<see cref="Command.TriggerFor"/>): the original where the
/// command was authored, else its own; the first edit on the other platform makes it that platform's own, the way steps work
/// (<see cref="Command.WithTriggerFor"/>). Changing between gesture and wheel keeps the keys and buttons held; choosing "No
/// trigger" unbinds, and ticking keys or buttons then makes it a click trigger (<see cref="Trigger.WithHold"/>).
/// </summary>
public sealed partial class CommandsViewModel
{
    private void SetTriggerKind(CommandItem command, TriggerKind kind)
    {
        switch (kind)
        {
            case TriggerKind.None:
                SetTrigger(command.Id, _ => Trigger.None);
                break;
            case TriggerKind.Wheel:
                try
                {
                    SetTrigger(command.Id, current => current is Trigger.WheelTrigger ? current : Trigger.ForWheel(WheelDirection.Up, Held(current)));
                }
                catch (MappingValidationException)
                {
                    // Wheel up is taken here (by the volume, say): the free direction rather than a refusal.
                    SetTrigger(command.Id, current => current is Trigger.WheelTrigger ? current : Trigger.ForWheel(WheelDirection.Down, Held(current)));
                }

                break;
            case TriggerKind.Gesture:
                _ = PickGestureAsync(command);
                break;
        }
    }

    private async Task PickGestureAsync(CommandItem command)
    {
        var current = RequireCommand(command.Id).Command.TriggerFor(_platform) is Trigger.GestureTrigger gesture ? gesture.GestureId : (GestureId?)null;
        var result = await _picker.PickAsync(current).ConfigureAwait(true);
        Guard(() =>
        {
            switch (result.Outcome)
            {
                case GesturePickerOutcome.Selected when result.GestureId is { } id:
                    SetTrigger(command.Id, trigger => Trigger.ForGesture(id, Held(trigger)));
                    break;
                case GesturePickerOutcome.NoGesture:
                    SetTrigger(command.Id, _ => Trigger.None);
                    break;
            }
        });
    }

    /// <summary>
    /// The keys and buttons kept when the kind changes between gesture, wheel and click: all of them, except that a wheel
    /// trigger held without the stroke button keeps only its keys, since a gesture draws with the stroke button.
    /// </summary>
    private static TriggerHold Held(Trigger trigger)
        => !trigger.IsBound ? TriggerHold.Default
            : trigger.Hold.HoldsStroke ? trigger.Hold
            : TriggerHold.WithStroke(trigger.Hold.Keys, capture: trigger.Hold.Capture);

    /// <summary>One store call: this platform's trigger changed by <paramref name="change"/>; says so when that made it this platform's own.</summary>
    private void SetTrigger(CommandId id, Func<Trigger, Trigger> change)
    {
        var (group, command) = RequireCommand(id);
        var trigger = change(command.TriggerFor(_platform));
        var forked = command.Origin is { } origin && origin != _platform && !command.HasOwnTriggerOn(_platform);
        _store.UpdateCommand(group.Id, command.WithTriggerFor(_platform, trigger, DateTimeOffset.UtcNow));
        if (forked)
        {
            Message = $"'{command.Name}' now has its own trigger and steps here; the original keeps running where it was authored. {CommandsKeymap.Current.Undo} undoes it.";
        }
    }
}
