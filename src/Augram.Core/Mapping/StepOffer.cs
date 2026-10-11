using System.Collections.ObjectModel;
using Augram.Core.Abstractions;
using Augram.Core.HoldRemaps;
using Augram.Core.Steps;
using Augram.Core.Steps.Remap;

namespace Augram.Core.Mapping;

/// <summary>
/// What a command's step editor may add (F5a "New step…", a paste, a duplicate) or change a step into (the step menu's "Change
/// to…", Joel 2026-10-11): whether a step can go into a command's steps on a platform now, and if not, why, as one short sentence
/// the App shows unchanged (the picker's greyed type, the "New step…" tooltip, the message line). Generic over step types: no type
/// is named but through <see cref="IStepType.HoldRemapsOnly"/> and the Remap step's default. Two parts, in this order:
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
/// output on a button trigger), so it is never refused for an output the editor would have fitted. A change is judged as a
/// replacement, not an addition (<see cref="CheckChange(IStep, Command, int, HostPlatform, Trigger?)"/>): the step's own type is
/// refused (<see cref="Already"/>), then the same two parts on the command with the new step in that step's place, so a Remap step
/// that is a command's only step may become a Hotkey step while a step among others may not become a Remap step. Pure; Mapping is
/// its home because a command's steps are Mapping's, and it reuses the hold remap part rather than re-implementing it.
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
        return Problem(step, command, platform, drafted, [.. command.StepsFor(platform), new CommandStep(step, platform)]);
    }

    /// <summary>
    /// The types of <paramref name="types"/> the command cannot take a step of now, each with its reason (<see cref="Check(IStepType, Command, HostPlatform, Trigger?)"/>);
    /// a type left out can be added. What the view model hands the step picker for the selected command.
    /// </summary>
    public static IReadOnlyDictionary<IStepType, string> Refusals(IEnumerable<IStepType> types, Command command, HostPlatform platform, Trigger? drafted = null)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(command);
        return RefusalsBy(types, type => Check(type, command, platform, drafted));
    }

    /// <summary>
    /// Why the step at <paramref name="index"/> of <paramref name="command"/>'s steps on <paramref name="platform"/> cannot become a
    /// step of <paramref name="type"/> now (the step menu's "Change to…"): "It is a Hotkey step already.", "A Remap step is a
    /// command's only step."; null when it can. The step checked is the one "New step…" would add (<see cref="NewStep"/>), in that
    /// step's place (<see cref="CheckChange(IStep, Command, int, HostPlatform, Trigger?)"/>).
    /// </summary>
    public static string? CheckChange(IStepType type, Command command, int index, HostPlatform platform, Trigger? drafted = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(command);
        return CheckChange(NewStep(type, command, platform, drafted), command, index, platform, drafted);
    }

    /// <summary>
    /// Why <paramref name="step"/> cannot take the place of the step at <paramref name="index"/> of <paramref name="command"/>'s
    /// steps on <paramref name="platform"/>, keeping that step's on/off state; null when it can. Judged as a replacement: a step of
    /// the type already there is refused (<see cref="Already"/>), then its type is checked (<see cref="NotHere"/>), then the step
    /// rules on the command with the step in that place.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a step of the command on <paramref name="platform"/>.</exception>
    public static string? CheckChange(IStep step, Command command, int index, HostPlatform platform, Trigger? drafted = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(command);
        var steps = command.StepsFor(platform);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, steps.Count);
        if (string.Equals(steps[index].Step.Type.Key, step.Type.Key, StringComparison.Ordinal))
        {
            return Already(step.Type);
        }

        var replaced = steps.ToList();
        replaced[index] = new CommandStep(step, platform, steps[index].IsActive);
        return Problem(step, command, platform, drafted, replaced);
    }

    /// <summary>
    /// The types of <paramref name="types"/> the step at <paramref name="index"/> cannot be changed to now, each with its reason
    /// (<see cref="CheckChange(IStepType, Command, int, HostPlatform, Trigger?)"/>; its own type always among them); a type left out
    /// can be chosen. What the view model hands the "Change to…" picker for that step.
    /// </summary>
    public static IReadOnlyDictionary<IStepType, string> ChangeRefusals(IEnumerable<IStepType> types, Command command, int index, HostPlatform platform, Trigger? drafted = null)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(command);
        return RefusalsBy(types, type => CheckChange(type, command, index, platform, drafted));
    }

    /// <summary>Why a step cannot be changed to its own type: "It is a Hotkey step already."</summary>
    public static string Already(IStepType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return $"It is a {type.DisplayName} step already.";
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

    /// <summary>
    /// The two parts for <paramref name="step"/> going into the command: where its type may appear (<see cref="NotHere"/>), then
    /// the step rules on the command with <paramref name="steps"/> (the step among them, added or in another's place) as this
    /// platform's steps.
    /// </summary>
    private static string? Problem(IStep step, Command command, HostPlatform platform, Trigger? drafted, IReadOnlyList<CommandStep> steps)
    {
        if (step.Type.HoldRemapsOnly && !TakesHoldRemapsOnly(command, drafted ?? command.TriggerFor(platform)))
        {
            return NotHere(step.Type);
        }

        // Never stored: the time only marks an own version made on the first edit away from the original, which no rule reads.
        return HoldRemapRules.StepsProblem(command.WithStepsFor(platform, steps, DateTimeOffset.UnixEpoch));
    }

    private static IReadOnlyDictionary<IStepType, string> RefusalsBy(IEnumerable<IStepType> types, Func<IStepType, string?> check)
    {
        var refusals = new Dictionary<IStepType, string>();
        foreach (var type in types)
        {
            if (check(type) is { } reason)
            {
                refusals[type] = reason;
            }
        }

        return refusals.Count == 0 ? ReadOnlyDictionary<IStepType, string>.Empty : refusals;
    }
}
