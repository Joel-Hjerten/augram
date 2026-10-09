using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Core.Mapping;

/// <summary>
/// Works out the <see cref="AnchorPlan"/> for a window (Joel, 2026-10-09: anchors are decided per app): which buttons are held
/// back there although they are not the stroke button, and which buttons join a press of each anchor. Only commands that
/// apply over the window count, as the resolver would see them: its app group's active commands used on this platform, plus
/// Global's unless the group suppresses globals, each with its trigger for this platform. A Global command whose trigger an
/// app command overlaps is shadowed, and a command that does nothing here (an override to nothing) holds nothing back, so
/// "Right + wheel up → nothing" in an app gives Right back to it. Pure; the engine's ignore-list watch asks it off the hook
/// thread when the window under the pointer or the mapping changes.
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
                if (command.IsActive && group.IsCommandUsedOn(command, platform) && command.TriggerFor(platform) is { IsBound: true } trigger && trigger.Hold.Physical != HeldButtons.None)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The plan over <paramref name="window"/> (null: nothing under the pointer, Global alone) on a machine whose stroke button is <paramref name="strokeButton"/>.</summary>
    public static AnchorPlan For(MappingDocument mapping, WindowIdentity? window, HostPlatform platform, MouseButton strokeButton)
        => ForGroup(mapping, CommandResolver.FindGroup(mapping, window, platform), platform, strokeButton);

    /// <summary>The plan over a window of <paramref name="group"/> (null: a window no app group claims).</summary>
    public static AnchorPlan ForGroup(MappingDocument mapping, AppGroup? group, HostPlatform platform, MouseButton strokeButton)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        var plan = AnchorPlan.None;
        var shadowing = new List<Trigger>();
        if (group is not null)
        {
            foreach (var (command, trigger) in Applying(group, platform))
            {
                shadowing.Add(trigger);
                plan = Add(plan, command, trigger, platform, strokeButton);
            }
        }

        if ((group is null || !group.SuppressGlobals) && mapping.Global.IsActive)
        {
            foreach (var (command, trigger) in Applying(mapping.Global, platform))
            {
                if (!shadowing.Exists(trigger.Overlaps))
                {
                    plan = Add(plan, command, trigger, platform, strokeButton);
                }
            }
        }

        return plan;
    }

    private static IEnumerable<(Command Command, Trigger Trigger)> Applying(AppGroup group, HostPlatform platform)
    {
        foreach (var command in group.Commands)
        {
            if (command.IsActive && group.IsCommandUsedOn(command, platform) && command.TriggerFor(platform) is { IsBound: true } trigger)
            {
                yield return (command, trigger);
            }
        }
    }

    private static AnchorPlan Add(AnchorPlan plan, Command command, Trigger trigger, HostPlatform platform, MouseButton strokeButton)
    {
        if (command.IsOverrideToNothingOn(platform))
        {
            return plan;
        }

        var hold = trigger.Hold.ForStrokeButton(strokeButton);
        if (hold.HoldsStroke)
        {
            return plan.WithExtras(strokeButton, ownerIsStroke: true, hold.Physical);
        }

        foreach (var anchor in hold.Physical.Buttons())
        {
            plan = plan.WithAnchor(anchor).WithExtras(anchor, ownerIsStroke: false, hold.Physical & ~anchor.Flag());
        }

        return plan;
    }
}
