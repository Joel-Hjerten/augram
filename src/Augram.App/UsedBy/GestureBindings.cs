using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.UsedBy;

/// <summary>
/// The mapping-side half of the Gestures tab's edits (F3 delete warning, A7 "Keep this"): the commands
/// bound to a gesture are unbound (<see cref="Trigger.None"/>; the command itself stays) or retargeted
/// to another gesture. Each command is one <see cref="MappingStore.UpdateCommand"/> call, so the Commands
/// tab undoes them one by one. Orchestration over the store, not a rule: the store's rules decide what is valid.
/// </summary>
public sealed class GestureBindings
{
    private readonly MappingStore _mapping;

    public GestureBindings(MappingStore mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        _mapping = mapping;
    }

    /// <summary>Every command bound to the gesture loses its trigger; returns how many did.</summary>
    public int Unbind(GestureId gestureId)
    {
        var rows = UsedByRow.For(_mapping, gestureId);
        foreach (var row in rows)
        {
            Unbind(row);
        }

        return rows.Count;
    }

    /// <summary>
    /// Every command bound to <paramref name="from"/> is bound to <paramref name="to"/> instead. One whose group
    /// already has a command on <paramref name="to"/> (one command per trigger per group) is unbound instead,
    /// so no command is left pointing at a gesture about to be deleted.
    /// </summary>
    public (int Retargeted, int Unbound) Retarget(GestureId from, GestureId to)
    {
        var retargeted = 0;
        var unbound = 0;
        foreach (var row in UsedByRow.For(_mapping, from))
        {
            if (TryRetarget(row, to))
            {
                retargeted++;
            }
            else
            {
                Unbind(row);
                unbound++;
            }
        }

        return (retargeted, unbound);
    }

    private bool TryRetarget(UsedByRow row, GestureId to)
    {
        if (_mapping.FindCommand(row.CommandId) is not { } pair)
        {
            return true;
        }

        try
        {
            _mapping.UpdateCommand(pair.Group.Id, pair.Command with { Trigger = Trigger.ForGesture(to) });
            return true;
        }
        catch (MappingValidationException)
        {
            return false;
        }
    }

    private void Unbind(UsedByRow row)
    {
        if (_mapping.FindCommand(row.CommandId) is { } pair)
        {
            _mapping.UpdateCommand(pair.Group.Id, pair.Command with { Trigger = Trigger.None });
        }
    }
}
