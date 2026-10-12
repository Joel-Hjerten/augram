using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands.Projection;

/// <summary>
/// A <see cref="Command"/> as one row of the command tree shows it (<see cref="CommandItem"/>, F5a): its trigger in words, its
/// step summary, its F8 lines and marker, as they read on the platform Augram runs on. The view model's half of the row; the
/// wording of trigger kinds and inputs stays with the components (<see cref="TriggerKindExtensions"/>, <see cref="InputKindExtensions"/>,
/// <see cref="StepPlatformMarker"/>).
/// </summary>
public static class CommandItemProjection
{
    /// <summary>
    /// Projects the command into its group's section, with no tag and no category choices;
    /// <paramref name="gesture"/> is the one its trigger on <paramref name="here"/> names (<see cref="Command.TriggerFor"/>),
    /// when it is one and the library still has it; the trigger, the step summary and the marker read as on <paramref name="here"/>.
    /// A trigger holding more than the stroke button leads the summary ("Shift + Undo · Minimize window").
    /// </summary>
    public static CommandItem From(AppGroup group, Command command, Gesture? gesture, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(command);
        var trigger = command.TriggerFor(here);
        var holdRemap = group.HoldRemapOf(command);
        var kind = TriggerKindExtensions.KindOf(trigger);
        var triggerText = holdRemap is null ? TriggerKindExtensions.Text(trigger, gesture?.Name ?? "Missing gesture", here) : InputKindExtensions.Text(trigger);
        var points = gesture is { Samples.Count: > 0 } ? gesture.Samples[0] : null;
        var usedHere = group.IsCommandUsedOn(command, here);
        var steps = Summarise(group, command, here);
        return new CommandItem(
            command.Id,
            group.Id,
            command.Name,
            command.IsActive,
            kind,
            triggerText,
            points,
            trigger.IsBound && !trigger.Hold.IsDefault ? $"{triggerText} · {steps}" : steps,
            usedHere ? StepPlatformMarker.ForCommand(command, here) : StepPlatformMarker.Only(group.EffectiveUseOn(command)))
        {
            Trigger = trigger,
            TriggerHint = holdRemap is null ? TriggerKindExtensions.Hint(trigger, here) : InputKindExtensions.Hint(holdRemap),
            AnchorWarning = TriggerKindExtensions.AnchorWarning(trigger, group),
            TriggerNote = TriggerLine(command, here),
            Section = holdRemap is null ? SectionId.ForGroup(group.Id) : SectionId.ForHoldRemap(group.Id, holdRemap.Id),
            CategoryId = command.CategoryId,
            HoldRemap = holdRemap,
            UseOn = command.UseOn,
            UseOnLimit = group.UseOnLimitFor(command),
            UseOnLimitText = LimitLine(group, command),
            IsElsewhere = !usedHere,
            IsOwnVersionStale = command.IsOwnVersionStale,
            CanSetNotIn = holdRemap is null,
            NotIn = command.NotIn,
            CanSetAlsoIn = holdRemap is null && trigger.IsBound && trigger.Hold.HandsBackDrags,
            AlsoIn = command.AlsoIn,
        };
    }

    /// <summary>"Set by category 'Personal': Windows only", "Set by app group 'Steam' and hold remap 'Space': …"; null when nothing limits the command.</summary>
    private static string? LimitLine(AppGroup group, Command command)
    {
        var limit = group.UseOnLimitFor(command);
        if (limit == PlatformSet.All)
        {
            return null;
        }

        var by = new List<string>(2);
        if (!group.IsGlobal && group.UseOn != PlatformSet.All)
        {
            by.Add($"app group '{group.Name}'");
        }

        if (group.CategoryOf(command) is { } category && category.UseOn != PlatformSet.All)
        {
            by.Add($"category '{category.Name}'");
        }

        if (group.HoldRemapOf(command) is { } holdRemap && holdRemap.UseOn != PlatformSet.All)
        {
            by.Add($"hold remap '{holdRemap.Name}'");
        }

        return $"Set by {string.Join(" and ", by)}: {StepPlatformMarker.Only(limit)}";
    }

    /// <summary>
    /// The header's line about this platform's trigger (F8, Joel 2026-10-09: keys convert like hotkeys, an edit here makes it
    /// this platform's own); null for a trigger authored here, or one whose keys need no conversion.
    /// </summary>
    private static string? TriggerLine(Command command, HostPlatform here)
    {
        var name = here.DisplayName();
        if (command.HasOwnTriggerOn(here))
        {
            return $"Own {name} trigger; the original keeps its own on {here.Other().DisplayName()}.";
        }

        if (command.Origin is not { } origin || origin == here)
        {
            return null;
        }

        var plan = command.TriggerPlanFor(here);
        if (plan.Trigger is null)
        {
            return $"{plan.Reason}. A trigger chosen here is {name}'s own.";
        }

        return plan.IsConverted
            ? $"{origin.DisplayName()} trigger {command.Trigger.Describe(origin)}, converted for {name}: {plan.Trigger.Describe(here)}. Changing it here makes it {name}'s own."
            : null;
    }

    /// <summary>The row's step summary here: "No steps" (Global) or "Does nothing here" (an app group's override to nothing), the one step's summary, or "3 steps".</summary>
    private static string Summarise(AppGroup group, Command command, HostPlatform here) => command.StepsFor(here).Count switch
    {
        0 => group.IsGlobal ? "No steps" : "Does nothing here",
        1 => StepPlatformMarker.For(command.StepsFor(here)[0], here).Summary,
        var n => $"{n} steps",
    };
}
