using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.App.Components.ListEditing;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The header's trigger and input edits of <see cref="CommandsViewModel"/> (F1 "Triggers as combinations", Joel 2026-10-09):
/// the kind (Gesture through the Select Gesture picker, Wheel, Button (plan 0005), No trigger), the wheel direction, a button
/// trigger's pressed button and the "While holding" set; under a hold remap (F9, plan 0002 step 4) the input that takes a
/// trigger's place (<see cref="Trigger.InputTrigger"/>). What is edited is this platform's trigger
/// (<see cref="Command.TriggerFor"/>): the original where the command was authored, else its own; the first edit on the other
/// platform makes it that platform's own, the way steps work (<see cref="Command.WithTriggerFor"/>). Changing between gesture
/// and wheel keeps the keys and buttons held; choosing "No trigger" unbinds, and ticking keys or buttons then makes it a click
/// trigger (<see cref="Trigger.WithHold"/>). Every edit goes through <see cref="EditTrigger"/>: it starts from the draft when
/// one waits, is saved as one <c>UpdateCommand</c> when the rules accept it, and otherwise waits as the draft with its note
/// (<c>.Draft</c>) instead of being refused; a draft another command uses here offers Swap and Take it (<c>.Conflict</c>).
/// The draft is the one piece of state it owns; the view model asks it for the header (<see cref="WithDraft"/>) and for the
/// trigger a step is offered against (<see cref="DraftedTrigger"/>).
/// </summary>
internal sealed partial class TriggerEditor
{
    private readonly MappingStore _store;
    private readonly GestureLibrary _gestures;
    private readonly IGesturePickerPresenter _picker;
    private readonly HostPlatform _platform;
    private readonly ICommandsHost _host;

    public TriggerEditor(MappingStore store, GestureLibrary gestures, IGesturePickerPresenter picker, HostPlatform platform, ICommandsHost host)
    {
        _store = store;
        _gestures = gestures;
        _picker = picker;
        _platform = platform;
        _host = host;
    }

    /// <summary>The header's trigger and input intents: the kind, the set, the wheel direction, the pressed button, an input, and the draft's Swap and Take it.</summary>
    public void Dispatch(CommandTreeActionEventArgs e)
    {
        if (e.Command is not { } command)
        {
            return;
        }

        switch (e.Action)
        {
            case CommandTreeAction.SetTriggerKind when e.Kind is { } kind:
                SetTriggerKind(command, kind);
                break;
            case CommandTreeAction.SetTriggerHold when e.Hold is { } hold:
                EditTrigger(command.Id, current => current.WithHold(hold));
                break;
            case CommandTreeAction.SetWheelDirection when e.Wheel is { } direction:
                EditTrigger(command.Id, current => Trigger.ForWheel(direction, current.Hold));
                break;
            case CommandTreeAction.SetTriggerButton when e.Button is { } pressed:
                EditTrigger(command.Id, current => Trigger.ForButton(pressed, current.Hold));
                break;
            case CommandTreeAction.SetInputKind when e.InputKind is { } inputKind:
                SetInputKind(command, inputKind);
                break;
            case CommandTreeAction.SetInput when e.Input is { } input:
                SetInput(command, input);
                break;
            case CommandTreeAction.SwapTrigger:
                SettleConflict(command.Id, take: false);
                break;
            case CommandTreeAction.TakeTrigger:
                SettleConflict(command.Id, take: true);
                break;
        }
    }

    /// <summary>The Select Gesture picker over the command's gesture (its draft's while one waits); a pick is a trigger edit.</summary>
    public async Task PickGestureAsync(CommandItem command)
    {
        var current = DraftedTrigger(command.Id) is Trigger.GestureTrigger gesture ? gesture.GestureId : (GestureId?)null;
        var result = await _picker.PickAsync(current).ConfigureAwait(true);
        _host.Guard(() =>
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
            case TriggerKind.Button:
                ChooseButton(command.Id);
                break;
            case TriggerKind.Gesture:
                _ = PickGestureAsync(command);
                break;
        }
    }

    /// <summary>
    /// Wheel up with the keys and buttons held now (the stroke button alone by default; a button trigger's set as it is, so
    /// Right + Left becomes Right + wheel up). When the rules refuse it (wheel up is the volume's here, say) the free direction is
    /// saved instead; when both are taken, wheel up waits as the draft with its note.
    /// </summary>
    private void ChooseWheel(CommandId id)
    {
        var current = DraftedTrigger(id);
        if (current is Trigger.WheelTrigger)
        {
            return;
        }

        var held = current is Trigger.ButtonTrigger ? current.Hold : Held(current);
        var up = Trigger.ForWheel(WheelDirection.Up, held);
        if (!TrySaveTrigger(id, up) && !TrySaveTrigger(id, Trigger.ForWheel(WheelDirection.Down, held)))
        {
            KeepDraft(id, up);
        }

        _host.ShowSelected();
    }

    /// <summary>
    /// A button trigger (plan 0005) pressed with Left, holding the keys and the buttons other than the stroke button held now
    /// (decision 2: never the stroke button; never the pressed button, <see cref="Trigger.ForButton"/> drops it). Saved when the
    /// rules take it; otherwise (no button left to hold, or the chord is another command's) it waits as the draft with its note.
    /// </summary>
    private void ChooseButton(CommandId id)
    {
        var current = DraftedTrigger(id);
        if (current is Trigger.ButtonTrigger)
        {
            return;
        }

        var held = current.IsBound ? current.Hold with { Buttons = current.Hold.Physical } : new TriggerHold(HeldButtons.None);
        EditTrigger(id, _ => Trigger.ForButton(MouseButton.Left, held));
    }

    /// <summary>
    /// An input's kind (F9): choosing Wheel takes wheel up, else the free direction, else waits as wheel up; Buttons and Key
    /// start empty, waiting for a button or a key, so a set being composed waits in the header instead of snapping back.
    /// </summary>
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

        _host.ShowSelected();
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

        _host.ShowSelected();
    }

    /// <summary>
    /// The keys and buttons kept when the kind changes between gesture, wheel and click: all of them, except that a wheel
    /// trigger held without the stroke button keeps only its keys, since a gesture draws with the stroke button.
    /// </summary>
    private static TriggerHold Held(Trigger trigger)
        => !trigger.IsBound ? TriggerHold.Default
            : trigger.Hold.HoldsStroke ? trigger.Hold
            : TriggerHold.WithStroke(trigger.Hold.Keys, capture: trigger.Hold.Capture);

    /// <summary>
    /// One store call (one undo step): this platform's trigger is <paramref name="trigger"/>; says so when that made it this
    /// platform's own, and when Core's normalisation cleared the command's Also in with it (plan 0005: a trigger holding the
    /// stroke button on both platforms never works over an excluded app).
    /// </summary>
    private void SaveTrigger(CommandId id, Trigger trigger)
    {
        var (group, command) = _store.RequireCommand(id);
        var forked = ForksHere(command);
        var stored = _store.UpdateCommand(group.Id, WithTriggerHere(command, trigger));
        var lines = new List<string>(2);
        if (forked)
        {
            lines.Add($"'{command.Name}' now has its own trigger and steps here; the original keeps running where it was authored.");
        }

        if (command.AlsoIn.Count > 0 && stored.AlsoIn.Count == 0)
        {
            lines.Add($"Cleared the Also in of '{command.Name}': only a trigger that holds a button other than the stroke button works over an excluded app.");
        }

        if (lines.Count > 0)
        {
            _host.Message = $"{string.Join(" ", lines)} {EditKeymap.Current.Undo} undoes it.";
        }
    }

    /// <summary>True when a trigger edit here makes the command this platform's own (F8: authored on the other platform, no own trigger here yet).</summary>
    private bool ForksHere(Command command)
        => command.Origin is { } origin && origin != _platform && !command.HasOwnTriggerOn(_platform);

    /// <summary>
    /// The command with <paramref name="trigger"/> as this platform's trigger, one edit: its steps are first fitted to it
    /// (<see cref="HoldRemapRules.FittedTo(Core.Steps.IStep, Trigger)"/>: on an input, a Remap step's button output on a wheel
    /// input becomes a notch the same way, a wheel output elsewhere Middle; on a button trigger, plan 0005 decision 8, a button
    /// or wheel output becomes a key output with no key yet), so changing the input or the trigger is never refused for the
    /// output, and the undo of it brings both back. Every trigger save and the draft's note go through this.
    /// </summary>
    private Command WithTriggerHere(Command command, Trigger trigger)
    {
        var now = DateTimeOffset.UtcNow;
        var steps = StepEditor.EditableSteps(command, _platform);
        var fitted = steps.Select(step => step with { Step = HoldRemapRules.FittedTo(step.Step, trigger) }).ToList();
        if (!fitted.SequenceEqual(steps))
        {
            command = command.WithStepsFor(_platform, fitted, now);
        }

        return command.WithTriggerFor(_platform, trigger, now);
    }
}
