using Augram.Core.Abstractions;

namespace Augram.Core.Mapping;

/// <summary>
/// Which command a trigger fires over a window (F5: global, then app override, then override to
/// nothing; plan 0001 M2 step 1). Pure: a document snapshot and the platform it runs on in, a resolution out; the
/// engine calls it on its worker with the snapshot it was last handed. Inactive groups, commands and ignored apps,
/// and app groups not used on this platform (F8 "Use on"), are invisible here; matchers match on this platform's
/// executable names or their known-app guess. The rule, in order:
/// <list type="number">
/// <item>the window belongs to an active ignored app → <see cref="ResolutionOutcome.Ignored"/>;</item>
/// <item>the first active app group (in document order) whose matcher matches the window is the app group;</item>
/// <item>an active command in that group with this trigger → matched ("app override in 'Chrome'", or "override to nothing in 'Steam'" when it has no steps);</item>
/// <item>else, if that group suppresses globals → none ("globals suppressed by 'FF7'");</item>
/// <item>else the Global group's active command for the trigger → matched ("global");</item>
/// <item>else none ("no command for this gesture").</item>
/// </list>
/// A null window (nothing under the point) skips 1 and 2 and resolves against Global alone.
/// </summary>
public static class CommandResolver
{
    public static CommandResolution Resolve(MappingDocument mapping, WindowIdentity? target, Trigger trigger, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(trigger);

        if (!trigger.IsBound)
        {
            return CommandResolution.None("no trigger");
        }

        var ignored = FindIgnored(mapping, target, platform);
        if (ignored is not null)
        {
            return CommandResolution.Ignored(ignored);
        }

        var group = FindGroup(mapping, target, platform);
        if (group is not null)
        {
            var command = ActiveCommandFor(group, trigger);
            if (command is not null)
            {
                return CommandResolution.Matched(group, command, command.IsOverrideToNothing
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

        var globalCommand = ActiveCommandFor(global, trigger);
        return globalCommand is not null
            ? CommandResolution.Matched(global, globalCommand, "global")
            : CommandResolution.None($"no command for {trigger.Describe()}");
    }

    /// <summary>The first active ignored app whose matcher claims the window, or null. The engine also asks this at button-down, before any stroke.</summary>
    public static IgnoredApp? FindIgnored(MappingDocument mapping, WindowIdentity? target, HostPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (target is null)
        {
            return null;
        }

        foreach (var app in mapping.Ignored)
        {
            if (app.IsActive && app.Matcher.Matches(target, platform))
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

    private static Command? ActiveCommandFor(AppGroup group, Trigger trigger)
    {
        foreach (var command in group.Commands)
        {
            if (command.IsActive && command.Trigger == trigger)
            {
                return command;
            }
        }

        return null;
    }
}
