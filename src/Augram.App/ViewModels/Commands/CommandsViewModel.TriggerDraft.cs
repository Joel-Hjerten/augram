using Augram.App.Components.CommandTree;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The trigger draft of <see cref="CommandsViewModel"/> (Joel, 2026-10-09: a combination is composed through states that are
/// not valid yet, such as Wheel chosen where both directions are taken, or the last button unticked before another is
/// ticked). A trigger edit the rules refuse does not snap back: it waits as the draft of the selected command's trigger, the
/// header shows it with a note saying what is wrong and how to fix it (<see cref="CommandItem.DraftNote"/>), the next edit
/// starts from it, and as soon as the rules accept it, it is saved as one normal store edit and the draft is gone. Only the
/// header shows a draft; the row keeps the stored trigger. It is UI state: selecting anything else drops it, and so does a
/// store change that removes the command or changes its stored trigger (an undo, a sync) or after which the rules accept it.
/// </summary>
public sealed partial class CommandsViewModel
{
    private TriggerDraft? _draft;

    /// <summary>The trigger the next edit of the command starts from: its draft while one waits, else what is stored here.</summary>
    private Trigger DraftedTrigger(CommandId id)
        => _draft is { } draft && draft.Id == id ? draft.Trigger : RequireCommand(id).Command.TriggerFor(_platform);

    /// <summary>
    /// Saves <paramref name="trigger"/> as the command's trigger here: one store edit (one undo step), or none when it is what is
    /// stored already; either way the draft is gone. False, with the store and the draft untouched, when the rules refuse it.
    /// </summary>
    private bool TrySaveTrigger(CommandId id, Trigger trigger)
    {
        var waiting = _draft;
        _draft = null;
        if (trigger.Normalised() == RequireCommand(id).Command.TriggerFor(_platform))
        {
            return true;
        }

        try
        {
            SaveTrigger(id, trigger);
            return true;
        }
        catch (MappingValidationException)
        {
            _draft = waiting;
            return false;
        }
    }

    /// <summary>Keeps <paramref name="trigger"/>, which the rules refused, as the draft over the command's stored trigger here.</summary>
    private void KeepDraft(CommandId id, Trigger trigger)
        => _draft = new TriggerDraft(id, RequireCommand(id).Command.TriggerFor(_platform), trigger.Normalised());

    /// <summary>Shows the selected command in the header again, with the draft over its trigger while one waits.</summary>
    private void ShowSelected() => SelectedCommand = WithDraft(SelectedRow());

    /// <summary>
    /// The selected row as the header shows it: with the draft over its trigger and the draft's note while one waits. A draft of
    /// another command, of one that is gone or whose stored trigger changed underneath it, or one the rules now accept, is dropped.
    /// </summary>
    private CommandItem? WithDraft(CommandItem? item)
    {
        if (_draft is not { } draft)
        {
            return item;
        }

        if (item is null
            || item.Id != draft.Id
            || _store.FindCommand(draft.Id) is not { } found
            || found.Command.TriggerFor(_platform) != draft.Stored
            || NoteFor(found.Group, found.Command, draft.Trigger) is not { } note)
        {
            _draft = null;
            return item;
        }

        var gesture = draft.Trigger is Trigger.GestureTrigger named ? _gestures.Find(named.GestureId) : null;
        var shown = item.WithDraft(draft.Trigger, gesture, found.Group, _platform, note);
        return StrokeButton is { } stroke ? CommandSections.WithStrokeButtonNote(shown, stroke) : shown;
    }

    /// <summary>
    /// Why the rules refuse <paramref name="trigger"/> as the command's trigger here, in plain words with how to fix it; null when
    /// they accept it. The rule is Core's (<see cref="MappingRules.EnsureValid(Command, AppGroup, IEnumerable{Command})"/>); this
    /// only finds the words: the button a wheel trigger lacks, or the command that already uses the trigger in the group (A7).
    /// </summary>
    private string? NoteFor(AppGroup group, Command command, Trigger trigger)
    {
        var candidate = MappingRules.Normalised(WithTriggerHere(command, trigger));
        var others = group.Commands.Where(other => other.Id != command.Id).ToList();
        try
        {
            MappingRules.EnsureValid(candidate, group, others);
            return null;
        }
        catch (MappingValidationException refusal)
        {
            if (trigger.IsBound && !trigger.Hold.HasAnchor)
            {
                return "Not saved yet: a wheel trigger needs the stroke button or another button held.";
            }

            foreach (var other in others)
            {
                if (MappingRules.Overlap(candidate, other) is { } phrase)
                {
                    // Taken on this platform reads as the header does; taken only on the other one says which ("… on macOS").
                    var uses = trigger.Overlaps(other.TriggerFor(_platform)) ? $"{Phrase(trigger)} here" : phrase;
                    return $"Not saved yet: '{other.Name}' already uses {uses}. {TriggerKindExtensions.FixHint(trigger)}";
                }
            }

            return $"Not saved yet: {refusal.Message}";
        }
    }

    private string Phrase(Trigger trigger)
    {
        var gesture = trigger is Trigger.GestureTrigger named ? _gestures.Find(named.GestureId)?.Name : null;
        return TriggerKindExtensions.Phrase(trigger, gesture ?? "Missing gesture", _platform);
    }

    /// <summary>A trigger the rules refused for one command, and the stored trigger it was drafted over.</summary>
    private sealed record TriggerDraft(CommandId Id, Trigger Stored, Trigger Trigger);
}
