using Augram.Core.Gestures;

namespace Augram.Core.Mapping;

/// <summary>
/// The mapping side of the Gestures tab's edits (F3 delete warning, A7 "Keep this"): the commands bound to a gesture are
/// unbound (<see cref="Trigger.None"/>; the command itself stays) or retargeted to another gesture, each action one change and
/// one undo step (plan 0007 Q8: one action, one undo). The rules decide what is valid, as for every other change.
/// </summary>
public sealed partial class MappingStore
{
    /// <summary>
    /// Every command bound to <paramref name="gestureId"/> loses that trigger (its own version's too), as one undo step; returns
    /// how many commands did. Commits nothing when none is bound.
    /// </summary>
    /// <exception cref="MappingValidationException">The result breaks a rule; nothing is changed.</exception>
    public int UnbindGesture(GestureId gestureId)
    {
        var next = Current;
        var unbound = 0;
        foreach (var (group, command) in BoundTo(next, gestureId))
        {
            next = Valid(next, group.Id, command.WithGestureReplaced(gestureId, null));
            unbound++;
        }

        if (unbound > 0)
        {
            Commit(next);
        }

        return unbound;
    }

    /// <summary>
    /// Every command bound to one of <paramref name="from"/> is bound to <paramref name="to"/> instead (the duplicates Keep this
    /// deletes), keeping the keys and buttons its trigger holds, as one undo step. One whose group already has a command on
    /// <paramref name="to"/> (one command per trigger per group) is unbound instead, so no command is left pointing at a gesture
    /// about to be deleted. The gestures are taken in the order given and each one's commands in document order, each seeing the
    /// ones before it. Commits nothing when no command is bound.
    /// </summary>
    /// <exception cref="MappingValidationException">Unbinding a command breaks a rule; nothing is changed.</exception>
    public (int Retargeted, int Unbound) RetargetGesture(IEnumerable<GestureId> from, GestureId to)
    {
        ArgumentNullException.ThrowIfNull(from);
        var next = Current;
        var retargeted = 0;
        var unbound = 0;
        foreach (var gestureId in from)
        {
            foreach (var (group, bound) in BoundTo(next, gestureId))
            {
                var command = next.FindCommand(bound.Id)?.Command ?? bound;
                try
                {
                    next = Valid(next, group.Id, command.WithGestureReplaced(gestureId, to));
                    retargeted++;
                }
                catch (MappingValidationException)
                {
                    next = Valid(next, group.Id, command.WithGestureReplaced(gestureId, null));
                    unbound++;
                }
            }
        }

        if (retargeted + unbound > 0)
        {
            Commit(next);
        }

        return (retargeted, unbound);
    }

    /// <summary>The commands bound to the gesture in <paramref name="document"/>, in document order, taken once (the list does not follow the changes made while walking it).</summary>
    private static (AppGroup Group, Command Command)[] BoundTo(MappingDocument document, GestureId gestureId)
        => [.. document.AllCommands().Where(pair => pair.Command.UsesGesture(gestureId))];

    /// <summary><paramref name="document"/> with the command of the same id in that group replaced, validated and sorted as a commit would.</summary>
    private static MappingDocument Valid(MappingDocument document, GroupId groupId, Command command)
        => MappingRules.ValidDocument(Replaced(document, groupId, command));
}
