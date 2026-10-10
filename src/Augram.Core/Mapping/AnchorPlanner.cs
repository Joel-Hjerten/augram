using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Core.Mapping;

/// <summary>
/// Works out the <see cref="AnchorPlan"/> for a window (Joel, 2026-10-09: anchors are decided per app): which buttons are held
/// back there although they are not the stroke button, and which buttons join a press of each anchor. Only commands that
/// apply over the window count, as the resolver would see them: its app group's active commands used on this platform, plus
/// Global's unless the group suppresses globals, each with its trigger for this platform. A Global command whose trigger an
/// app command overlaps is shadowed, and a command that does nothing here (an override to nothing) holds nothing back, so
/// "Right + wheel up → nothing" in an app gives Right back to it, as does a command whose "Not in" names an Ignored › Per command
/// entry claiming the window (plan 0004; such a command shadows nothing either). It also says how far each anchor's press may move before it is handed back as a drag (<see cref="AnswerForGroup"/>).
/// Pure; the engine's ignore-list watch asks it off the hook thread when the window under the pointer or the mapping changes.
/// </summary>
public static class AnchorPlanner
{
    /// <summary>True when some active command here holds a button beyond the stroke button: the hook then needs a plan per window.</summary>
    public static bool UsesButtons(MappingDocument mapping, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        foreach (var group in mapping.Groups)
        {
            if (!group.IsActive || !group.IsUsedOn(platform))
            {
                continue;
            }

            foreach (var command in group.Commands)
            {
                if (command.IsActive && command.HoldRemapId is null && group.IsCommandUsedOn(command, platform) && command.TriggerFor(platform) is { IsBound: true } trigger && trigger.Hold.Physical != HeldButtons.None)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The plan over <paramref name="window"/> (null: nothing under the pointer, Global alone) on a machine whose stroke button is <paramref name="strokeButton"/>.</summary>
    public static AnchorPlan For(MappingDocument mapping, WindowIdentity? window, HostPlatform platform, MouseButton strokeButton)
        => AnswerForGroup(mapping, CommandResolver.FindGroup(mapping, window, platform), platform, strokeButton, IgnoreList.PerCommandUnder(mapping, window, platform)).Plan;

    /// <summary>The plan over a window of <paramref name="group"/> (null: a window no app group claims).</summary>
    public static AnchorPlan ForGroup(MappingDocument mapping, AppGroup? group, HostPlatform platform, MouseButton strokeButton)
        => AnswerForGroup(mapping, group, platform, strokeButton).Plan;

    /// <summary>
    /// The plan over a window of <paramref name="group"/> and, for each anchor in it, the drag distance its commands set (plan
    /// 0004): the largest own distance, with the Options value counted for a command that sets none. One pass over the same
    /// commands, so the two always agree. <paramref name="perCommand"/> are the Ignored › Per command entries claiming the window
    /// (<see cref="IgnoreList.PerCommandUnder"/>; none when null): a command naming one in its "Not in" counts as absent.
    /// </summary>
    public static (AnchorPlan Plan, AnchorDragDistances Drags) AnswerForGroup(
        MappingDocument mapping, AppGroup? group, HostPlatform platform, MouseButton strokeButton, IReadOnlyList<GroupId>? perCommand = null)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        var plan = AnchorPlan.None;
        var drags = AnchorDragDistances.None;
        foreach (var (command, trigger) in Holding(mapping, group, perCommand ?? [], platform))
        {
            (plan, drags) = Add(plan, drags, command, trigger, platform, strokeButton);
        }

        return (plan, drags);
    }

    /// <summary>
    /// The commands that apply over a window of <paramref name="group"/>, as the resolver would see them: the group's, then
    /// Global's unless the group suppresses globals, without a Global command an app command overlaps (shadowed), and without
    /// any command whose "Not in" names one of <paramref name="perCommand"/> (plan 0004: as if it did not exist, so it shadows
    /// nothing either).
    /// </summary>
    private static IEnumerable<(Command Command, Trigger Trigger)> Holding(MappingDocument mapping, AppGroup? group, IReadOnlyList<GroupId> perCommand, HostPlatform platform)
    {
        var shadowing = new List<Trigger>();
        if (group is not null)
        {
            foreach (var (command, trigger) in Applying(group, platform))
            {
                if (!command.IsNotIn(perCommand))
                {
                    shadowing.Add(trigger);
                    yield return (command, trigger);
                }
            }
        }

        if ((group is null || !group.SuppressGlobals) && mapping.Global.IsActive)
        {
            foreach (var (command, trigger) in Applying(mapping.Global, platform))
            {
                if (!shadowing.Exists(trigger.Overlaps) && !command.IsNotIn(perCommand))
                {
                    yield return (command, trigger);
                }
            }
        }
    }

    /// <summary>The commands that hold buttons back here; a command under a hold remap never does (F9: its hold remap owns its input).</summary>
    private static IEnumerable<(Command Command, Trigger Trigger)> Applying(AppGroup group, HostPlatform platform)
    {
        foreach (var command in group.Commands)
        {
            if (command.IsActive && command.HoldRemapId is null && group.IsCommandUsedOn(command, platform) && command.TriggerFor(platform) is { IsBound: true } trigger)
            {
                yield return (command, trigger);
            }
        }
    }

    private static (AnchorPlan, AnchorDragDistances) Add(AnchorPlan plan, AnchorDragDistances drags, Command command, Trigger trigger, HostPlatform platform, MouseButton strokeButton)
    {
        if (command.IsOverrideToNothingOn(platform))
        {
            return (plan, drags);
        }

        var hold = trigger.Hold.ForStrokeButton(strokeButton);
        if (hold.HoldsStroke)
        {
            return (plan.WithExtras(strokeButton, ownerIsStroke: true, hold.Physical), drags);
        }

        foreach (var anchor in hold.Physical.Buttons())
        {
            plan = plan.WithAnchor(anchor).WithExtras(anchor, ownerIsStroke: false, hold.Physical & ~anchor.Flag());
            drags = hold.DragDistancePx is { } own ? drags.WithOwn(anchor, own) : drags.WithOptionsValue(anchor);
        }

        return (plan, drags);
    }
}
