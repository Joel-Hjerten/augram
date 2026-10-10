using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// Which command a trigger fires over a window (F5: global, then app override, then override to
/// nothing; plan 0001 M2 step 1). Pure: a document snapshot and the platform it runs on in, a resolution out; the
/// engine calls it on its worker with the snapshot it was last handed. Inactive groups, commands and ignored apps,
/// and app groups and commands not used on this platform (F8 "Use on": a command counts as used only when its group, its
/// category and itself all include the platform, <see cref="AppGroup.IsCommandUsedOn"/>), are invisible here; matchers match
/// on this platform's executable names or their known-app guess. The rule, in order:
/// <list type="number">
/// <item>the window belongs to an active ignored app on Ignored › Global → <see cref="ResolutionOutcome.Ignored"/>, unless (plan
/// 0005 decision 7) a command whose "Also in" names that entry matches by the steps below: it fires, reason "global, also in 'Blender'";</item>
/// <item>the first active app group (in document order) whose matcher matches the window is the app group;</item>
/// <item>an active command in that group whose trigger on this platform matches the press exactly (the keys and buttons held, learnings 0003 §3.4; there is no fallback to a trigger holding fewer) → matched ("app override in 'Chrome'", or "override to nothing in 'Steam'" when it has no steps);</item>
/// <item>else, if that group suppresses globals → none ("globals suppressed by 'FF7'");</item>
/// <item>else the Global group's active command for the trigger → matched ("global");</item>
/// <item>else none ("no command for this gesture").</item>
/// </list>
/// A command whose "Not in" names an Ignored › Per command entry that claims the window (plan 0004) is as if it did not exist
/// in steps 3 and 5: an app group's falls through to Global's, and when nothing else fires the reason says so
/// ("'Zoom In' is not used in 'Spine'").
/// A null window (nothing under the point) skips 1 and 2 and resolves against Global alone. Commands under a hold remap
/// (F9) are never resolved here: the hold remap plays them while its hold key is held.
/// </summary>
public static class CommandResolver
{
    /// <summary>The press a configured trigger describes (<see cref="PressedTrigger.Of"/>), resolved: for callers that hold a trigger rather than a press.</summary>
    public static CommandResolution Resolve(MappingDocument mapping, WindowIdentity? target, Trigger trigger, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return Resolve(mapping, target, PressedTrigger.Of(trigger), platform);
    }

    /// <summary>The rule above for one press; a command matches when its trigger on <paramref name="platform"/> (<see cref="Command.TriggerFor"/>) matches the press exactly.</summary>
    public static CommandResolution Resolve(MappingDocument mapping, WindowIdentity? target, PressedTrigger trigger, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(trigger);

        if (!trigger.IsBound)
        {
            return CommandResolution.None("no trigger");
        }

        var ignored = FindIgnored(mapping, target, platform);
        if (ignored is null)
        {
            return ResolveCommands(mapping, target, trigger, platform, null);
        }

        // Over an excluded app only the commands whose "Also in" names it apply (plan 0005 decision 7); nothing else fires there.
        var also = ignored.DisableEntirely ? null : ResolveCommands(mapping, target, trigger, platform, ignored.Id);
        return also is { Outcome: ResolutionOutcome.Matched }
            ? also with { Reason = $"{also.Reason}, also in '{ignored.Name}'" }
            : CommandResolution.Ignored(ignored);
    }

    /// <summary>Steps 2 to 6 of the rule; over an excluded app (<paramref name="excludedBy"/>), only commands whose "Also in" names it count.</summary>
    private static CommandResolution ResolveCommands(MappingDocument mapping, WindowIdentity? target, PressedTrigger trigger, HostPlatform platform, GroupId? excludedBy)
    {
        var group = FindGroup(mapping, target, platform);
        var perCommand = IgnoreList.PerCommandUnder(mapping, target, platform);
        string? notUsed = null;
        if (group is not null)
        {
            var command = ActiveCommandFor(group, trigger, platform, excludedBy);
            if (command is not null && command.IsNotIn(perCommand))
            {
                notUsed = NotUsed(mapping, command, perCommand);
                command = null;
            }

            if (command is not null)
            {
                return CommandResolution.Matched(group, command, command.IsOverrideToNothingOn(platform)
                    ? $"override to nothing in '{group.Name}'"
                    : $"app override in '{group.Name}'");
            }

            if (group.SuppressGlobals)
            {
                return CommandResolution.None($"globals suppressed by '{group.Name}'");
            }
        }

        var global = mapping.Global;
        if (!global.IsActive)
        {
            return CommandResolution.None("the Global group is inactive");
        }

        var globalCommand = ActiveCommandFor(global, trigger, platform, excludedBy);
        if (globalCommand is not null && globalCommand.IsNotIn(perCommand))
        {
            return CommandResolution.None(NotUsed(mapping, globalCommand, perCommand));
        }

        return globalCommand is not null
            ? CommandResolution.Matched(global, globalCommand, "global")
            : CommandResolution.None(notUsed ?? $"no command for {trigger.Describe()}");
    }

    /// <summary>"'Zoom In' is not used in 'Spine'": the first Per command entry claiming the window that the command names.</summary>
    private static string NotUsed(MappingDocument mapping, Command command, IReadOnlyList<GroupId> perCommand)
    {
        var app = mapping.Ignored.First(app => perCommand.Contains(app.Id) && command.NotIn.Contains(app.Id));
        return $"'{command.Name}' is not used in '{app.Name}'";
    }

    /// <summary>
    /// The first active ignored app on Ignored › Global (either mode) whose matcher claims the window, or null; a Per command
    /// entry stops only the commands that name it (plan 0004). <see cref="IgnoreList.Under"/> is
    /// this rule: the engine's ignore-list watch asks it as the pointer moves, so the stroke button passes through over the app
    /// before any stroke; here it is the backstop for a stroke captured on a stale answer.
    /// </summary>
    public static IgnoredApp? FindIgnored(MappingDocument mapping, WindowIdentity? target, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (target is null)
        {
            return null;
        }

        foreach (var app in mapping.Ignored)
        {
            if (app.IsActive && !app.IsPerCommand && app.Matcher.Matches(target, platform))
            {
                return app;
            }
        }

        return null;
    }

    /// <summary>The first active app group (never Global) used on <paramref name="platform"/> whose matcher claims the window, or null.</summary>
    public static AppGroup? FindGroup(MappingDocument mapping, WindowIdentity? target, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (target is null)
        {
            return null;
        }

        foreach (var group in mapping.Groups)
        {
            if (group.IsActive && !group.IsGlobal && group.IsUsedOn(platform) && group.Matcher is not null && group.Matcher.Matches(target, platform))
            {
                return group;
            }
        }

        return null;
    }

    /// <summary>The active command for the press; commands under a hold remap are skipped (F9: their inputs belong to the hold remap), and over an excluded app every command whose "Also in" does not name it.</summary>
    private static Command? ActiveCommandFor(AppGroup group, PressedTrigger trigger, HostPlatform platform, GroupId? excludedBy)
    {
        foreach (var command in group.Commands)
        {
            if (command.IsActive && command.HoldRemapId is null && group.IsCommandUsedOn(command, platform) && (excludedBy is not { } app || command.IsAlsoIn(app)) && trigger.Matches(command.TriggerFor(platform)))
            {
                return command;
            }
        }

        return null;
    }
}
