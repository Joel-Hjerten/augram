using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Remap;

namespace Augram.Core.Mapping;

/// <summary>One window's answer (<see cref="AnchorPlanner.Answer"/>): the anchor plan, the drag distances, and the button trigger outputs the worker holds.</summary>
public sealed record AnchorAnswer(AnchorPlan Plan, AnchorDragDistances Drags, ButtonOutputs Outputs)
{
    public static AnchorAnswer None { get; } = new(AnchorPlan.None, AnchorDragDistances.None, ButtonOutputs.Empty);
}

/// <summary>
/// Works out the <see cref="AnchorPlan"/> for a window (Joel, 2026-10-09: anchors are decided per app): which buttons are held
/// back there although they are not the stroke button, and which buttons join a press of each anchor. Only commands that
/// apply over the window count, as the resolver would see them: its app group's active commands used on this platform, plus
/// Global's unless the group suppresses globals, each with its trigger for this platform. A Global command whose trigger an
/// app command overlaps is shadowed, and a command that does nothing here (an override to nothing) holds nothing back, so
/// "Right + wheel up → nothing" in an app gives Right back to it, as does a command whose "Not in" names an Ignored › Per command
/// entry claiming the window (plan 0004; such a command shadows nothing either). It also says how far each anchor's press may move before it is handed back as a drag (<see cref="AnswerForGroup"/>).
/// A button trigger (plan 0005, "Right + Left") holds its anchors back, takes its pressed button into their presses and marks it
/// firing for them; <see cref="Answer"/> also lists the button trigger commands whose Remap key output the engine worker holds.
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
        var answer = Answer(mapping, group, platform, strokeButton, perCommand);
        return (answer.Plan, answer.Drags);
    }

    /// <summary>
    /// <see cref="AnswerForGroup"/> with the button trigger outputs the engine worker holds itself over the same window (plan 0005
    /// decision 9), from the same commands in the same order, so the three always agree. Over a window an Exclusions › Global entry
    /// claims, <paramref name="excludedBy"/> is that entry: only the commands whose "Also in" names it apply there (plan 0005
    /// decision 7), so the plan holds back their anchors alone, and none of the stroke button's extras.
    /// </summary>
    public static AnchorAnswer Answer(
        MappingDocument mapping, AppGroup? group, HostPlatform platform, MouseButton strokeButton, IReadOnlyList<GroupId>? perCommand = null, GroupId? excludedBy = null)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        var plan = AnchorPlan.None;
        var drags = AnchorDragDistances.None;
        List<ButtonOutput>? outputs = null;
        foreach (var (command, trigger) in Holding(mapping, group, perCommand ?? [], platform, excludedBy))
        {
            if (excludedBy is not null && trigger.Hold.ForStrokeButton(strokeButton).HoldsStroke)
            {
                // An excluded app keeps its stroke button: an "Also in" on a trigger that holds it here holds nothing back.
                continue;
            }

            (plan, drags) = Add(plan, drags, command, trigger, platform, strokeButton);
            if (OutputOf(command, trigger, platform, strokeButton) is { } output)
            {
                (outputs ??= []).Add(output);
            }
        }

        return new AnchorAnswer(plan, drags, outputs is null ? ButtonOutputs.Empty : ButtonOutputs.Of(outputs));
    }

    /// <summary>
    /// The commands that apply over a window of <paramref name="group"/>, as the resolver would see them: the group's, then
    /// Global's unless the group suppresses globals, without a Global command an app command overlaps (shadowed), and without
    /// any command whose "Not in" names one of <paramref name="perCommand"/> (plan 0004: as if it did not exist, so it shadows
    /// nothing either). Over an excluded app (<paramref name="excludedBy"/>), only commands whose "Also in" names it exist.
    /// </summary>
    private static IEnumerable<(Command Command, Trigger Trigger)> Holding(MappingDocument mapping, AppGroup? group, IReadOnlyList<GroupId> perCommand, HostPlatform platform, GroupId? excludedBy)
    {
        var shadowing = new List<Trigger>();
        if (group is not null)
        {
            foreach (var (command, trigger) in Applying(group, platform))
            {
                if (!command.IsNotIn(perCommand) && (excludedBy is not { } app || command.IsAlsoIn(app)))
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
                if (!shadowing.Exists(trigger.Overlaps) && !command.IsNotIn(perCommand) && (excludedBy is not { } app || command.IsAlsoIn(app)))
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
        var fires = trigger as Trigger.ButtonTrigger;
        if (hold.HoldsStroke)
        {
            // A button trigger never anchors on the stroke button (plan 0005 decision 2): one that names this machine's holds nothing back here.
            return fires is null ? (plan.WithExtras(strokeButton, ownerIsStroke: true, hold.Physical), drags) : (plan, drags);
        }

        foreach (var anchor in hold.Physical.Buttons())
        {
            // A button trigger's pressed button joins its anchors' presses like a held member, and fires there at once.
            var extras = (hold.Physical & ~anchor.Flag()) | (fires is null ? HeldButtons.None : fires.Button.Flag());
            plan = plan.WithAnchor(anchor).WithExtras(anchor, ownerIsStroke: false, extras);
            plan = fires is null ? plan : plan.WithFires(anchor, fires.Button);
            drags = hold.DragDistancePx is { } own ? drags.WithOwn(anchor, own) : drags.WithOptionsValue(anchor);
        }

        return (plan, drags);
    }

    /// <summary>
    /// A button trigger command that holds a key while its buttons are down (plan 0005 decisions 8 and 9): its one active step is
    /// a Remap step with a key set. Null for anything else, which the executor runs at the press as for a wheel trigger.
    /// </summary>
    private static ButtonOutput? OutputOf(Command command, Trigger trigger, HostPlatform platform, MouseButton strokeButton)
    {
        if (trigger is not Trigger.ButtonTrigger || command.IsOverrideToNothingOn(platform) || trigger.Hold.ForStrokeButton(strokeButton).HoldsStroke)
        {
            return null;
        }

        var active = command.PlanFor(platform).Where(step => step.Stored.IsActive).ToArray();
        return active is [{ Run.Step: RemapStep { Output: RemapOutput.Key { IsSet: true } key } }]
            ? new ButtonOutput(command.Id, command.Name, trigger, key)
            : null;
    }
}
