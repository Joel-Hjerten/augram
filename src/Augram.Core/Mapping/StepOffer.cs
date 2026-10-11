using System.Collections.ObjectModel;
using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;

namespace Augram.Core.Mapping;

/// <summary>
/// What a command's step editor may add (F5a "New step…", a paste, a duplicate): whether a step can go into a command's steps on
/// a platform now, and if not, why, as one short sentence the App shows unchanged (the picker's greyed type, the "New step…"
/// tooltip, the message line). Generic over step types: no type is named but through <see cref="IStepType.HoldRemapsOnly"/> and
/// the Remap step's default. Two parts, in this order:
/// <list type="number">
/// <item>Where a type may appear at all: a <see cref="IStepType.HoldRemapsOnly"/> type (the Remap step) only for a command under a
/// hold remap (plan 0002) or with a button trigger here (plan 0005 decision 8), <see cref="NotHere"/>. This is the offer's own
/// rule: loading never checks it, so a Remap step left on an ordinary command (pasted out of a hold remap) still loads and is
/// skipped when it runs.</item>
/// <item>The step rules validation runs (<see cref="HoldRemapRules.StepsProblem"/>: a Remap step is a command's only step; its
/// output fits the trigger), asked of the command with the step added, on both platforms, so the offer refuses exactly what the
/// store would and with the same reason.</item>
/// </list>
/// A type is checked with the step "New step…" would add (<see cref="NewStep"/>: a Remap step fitted to the command's input, a key
/// output on a button trigger), so it is never refused for an output the editor would have fitted. Pure; Mapping is its home
/// because a command's steps are Mapping's, and it reuses the hold remap part rather than re-implementing it.
/// </summary>
public static class StepOffer
{
    /// <summary>
    /// Why a step of <paramref name="type"/> cannot be added to <paramref name="command"/>'s steps on <paramref name="platform"/>
    /// now ("A Remap step is a command's only step.", "Remap is for a command under a hold remap or with a button trigger."); null
    /// when it can. <paramref name="drafted"/> is the trigger the editor shows there, a trigger draft waiting over the stored one
    /// (null: the stored one); it decides where a type may appear and the new step's output, as the header shows the command.
    /// </summary>
    public static string? Check(IStepType type, Command command, HostPlatform platform, Trigger? drafted = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(command);
        return Check(NewStep(type, command, platform, drafted), command, platform, drafted);
    }

    /// <summary>
    /// Why <paramref name="step"/> (a copied step pasted, a step duplicated) cannot be added to <paramref name="command"/>'s steps
    /// on <paramref name="platform"/>; null when it can. Its type is checked first (<see cref="NotHere"/>), then the step rules on
    /// the command with the step at the end of this platform's steps (where it goes among them changes no rule).
    /// </summary>
    public static string? Check(IStep step, Command command, HostPlatform platform, Trigger? drafted = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(command);
        if (step.Type.HoldRemapsOnly && !TakesHoldRemapsOnly(command, drafted ?? command.TriggerFor(platform)))
        {
            return NotHere(step.Type);
        }

        // Never stored: the time only marks an own version made on the first edit away from the original, which no rule reads.
        var candidate = command.WithStepsFor(platform, [.. command.StepsFor(platform), new CommandStep(step, platform)], DateTimeOffset.UnixEpoch);
        return HoldRemapRules.StepsProblem(candidate);
    }

    /// <summary>
    /// The types of <paramref name="types"/> the command cannot take a step of now, each with its reason (<see cref="Check(IStepType, Command, HostPlatform, Trigger?)"/>);
    /// a type left out can be added. What the view model hands the step picker for the selected command.
    /// </summary>
    public static IReadOnlyDictionary<IStepType, string> Refusals(IEnumerable<IStepType> types, Command command, HostPlatform platform, Trigger? drafted = null)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(command);
        var refusals = new Dictionary<IStepType, string>();
        foreach (var type in types)
        {
            if (Check(type, command, platform, drafted) is { } reason)
            {
                refusals[type] = reason;
            }
        }

        return refusals.Count == 0 ? ReadOnlyDictionary<IStepType, string>.Empty : refusals;
    }

    /// <summary>
    /// The step "New step…" adds of <paramref name="type"/> to the command on <paramref name="platform"/>: the type's default; a
    /// Remap step's output fitted to the command's stored input there (<see cref="HoldRemapRules.FittedTo"/>: a wheel input starts
    /// with a wheel notch the same way), or, on a button trigger (<paramref name="drafted"/>, the header's, a draft included; plan
    /// 0005), a key output with no key set; so it is never refused for its output.
    /// </summary>
    public static IStep NewStep(IStepType type, Command command, HostPlatform platform, Trigger? drafted = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(command);
        var step = type.CreateDefault();
        if (step is not RemapStep remap)
        {
            return step;
        }

        return command.TriggerFor(platform) is Trigger.InputTrigger { Input: var input } ? new RemapStep(HoldRemapRules.FittedTo(remap.Output, input))
            : (drafted ?? command.TriggerFor(platform)) is Trigger.ButtonTrigger ? new RemapStep(new RemapOutput.Key(KeyCode.None))
            : step;
    }

    /// <summary>Why a <see cref="IStepType.HoldRemapsOnly"/> type is not offered here: "Remap is for a command under a hold remap or with a button trigger."</summary>
    public static string NotHere(IStepType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return $"{type.DisplayName} is for a command under a hold remap or with a button trigger.";
    }

    /// <summary>A command under a hold remap, or one whose trigger here is a button trigger (plan 0005: its key is held while both buttons are down).</summary>
    private static bool TakesHoldRemapsOnly(Command command, Trigger trigger)
        => command.HoldRemapId is not null || trigger is Trigger.ButtonTrigger;
}
