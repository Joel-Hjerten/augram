using Augram.App.Components.CommandTree;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// Swap and Take it on a trigger draft (Joel, 2026-10-10: inverting Zoom In and Zoom Out; what shortcut editors do). When the
/// draft is refused only because another command of the group (a sibling: the group's ordinary commands, or the hold remap's)
/// uses it on this platform, the header's note offers to settle it in one edit: Swap gives the other command this command's
/// stored trigger (or input) here; Take it leaves the other with none here and selects it, so it can be given a new one. Each
/// is one <c>UpdateGroup</c>, so one undo step brings both back, and each command changes the way any trigger edit changes it
/// (<c>WithTriggerHere</c>: an input brings its Remap output along, a command authored on the other platform gets its own
/// version here, and the message says so). Each is offered only when a dry run of the group through Core's rules passes
/// (<see cref="Accepts"/>), so neither is a button that is then refused.
/// </summary>
public sealed partial class CommandsViewModel
{
    /// <summary>The draft's header item with what its note offers against <paramref name="other"/>, the command that uses the draft here.</summary>
    private CommandItem WithConflict(CommandItem shown, AppGroup group, Command command, TriggerDraft draft, Command other)
    {
        var canSwap = draft.Stored.IsBound && Accepts(Exchanged(group, command, draft.Trigger, other, draft.Stored));
        var canTake = Accepts(Exchanged(group, command, draft.Trigger, other, Trigger.None));
        return shown.WithConflict(other.Name, canSwap, canTake);
    }

    /// <summary>
    /// Swap (<paramref name="take"/> false) or Take it: the command's draft is saved and the command that uses it here gets this
    /// command's stored trigger, or none; one store call. Take it then selects that command, its section opened (and the other
    /// platforms' commands listed when this platform leaves it out). A draft that is gone or no longer refused by another
    /// command here (a store change meanwhile) does nothing but show the header again.
    /// </summary>
    private void SettleConflict(CommandId id, bool take)
    {
        var (group, command) = RequireCommand(id);
        if (_draft is not { } draft
            || draft.Id != id
            || command.TriggerFor(_platform) != draft.Stored
            || RefusalOf(group, command, draft.Trigger)?.TakenBy is not { } other)
        {
            ShowSelected();
            return;
        }

        var forked = new[] { command, other }.Where(ForksHere).Select(each => each.Name).ToList();
        var theirs = take ? Trigger.None : draft.Stored;
        _store.UpdateGroup(Exchanged(group, command, draft.Trigger, other, theirs));
        _draft = null;
        var undo = $"{ForkLine(forked)} {CommandsKeymap.Current.Undo} undoes both.";
        if (take)
        {
            // A7 counts a command this platform leaves out too; the list then shows it greyed, so it can be selected.
            ShowOtherPlatforms |= !group.IsCommandUsedOn(other, _platform);
            ShowCommand(other.Id);
            Message = $"'{other.Name}' has no {(group.HoldRemapOf(other) is null ? "trigger" : "input")} now: give it one.{undo}";
        }
        else
        {
            Message = $"Swapped with '{other.Name}', which now uses {Phrase(theirs)}.{undo}";
        }
    }

    /// <summary>
    /// The group with <paramref name="mine"/> as the command's trigger here and <paramref name="theirs"/> as
    /// <paramref name="other"/>'s, each made the way a trigger edit makes it (<see cref="WithTriggerHere"/>).
    /// </summary>
    private AppGroup Exchanged(AppGroup group, Command command, Trigger mine, Command other, Trigger theirs)
        => group with
        {
            Commands = [.. group.Commands.Select(each => each.Id == command.Id ? WithTriggerHere(each, mine) : each.Id == other.Id ? WithTriggerHere(each, theirs) : each)],
        };

    /// <summary>The dry run: whether the store would take <paramref name="next"/> as its group (Core's rules, as a commit checks a group).</summary>
    private bool Accepts(AppGroup next)
    {
        try
        {
            MappingRules.EnsureValid(MappingRules.Normalised(next), _store.Current.Groups.Where(group => group.Id != next.Id));
            return true;
        }
        catch (MappingValidationException)
        {
            return false;
        }
    }

    /// <summary>The message's words for the commands the edit made this platform's own (F8), as <c>SaveTrigger</c> says it for one.</summary>
    private static string ForkLine(List<string> forked) => forked.Count switch
    {
        0 => string.Empty,
        1 => $" '{forked[0]}' now has its own trigger and steps here; the original keeps running where it was authored.",
        _ => $" '{forked[0]}' and '{forked[1]}' now have their own triggers and steps here; the originals keep running where they were authored.",
    };
}
