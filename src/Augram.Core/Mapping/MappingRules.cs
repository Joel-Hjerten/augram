using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;

namespace Augram.Core.Mapping;

/// <summary>
/// The business rules of the mapping (ADR-0002 §5a: one home, called by the store; view models only display the outcome).
/// Names compare trimmed and case-insensitively, like gesture names. Group, command and category order is not a user choice
/// (F5a): <see cref="ValidDocument"/> sorts groups Global first and then by name, and commands and categories by name, so
/// every snapshot the store hands out is already in display order. A group's categories follow <see cref="CategoryRules"/>,
/// its hold remaps and the commands under them <see cref="HoldRemapRules"/> (F9).
/// </summary>
public static class MappingRules
{
    public static StringComparer NameComparer => GestureRules.NameComparer;

    /// <summary>Normalises and validates a whole document (a load, an import, every store commit) and returns it sorted.</summary>
    public static MappingDocument ValidDocument(MappingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var groups = new List<AppGroup>();
        foreach (var group in document.Groups)
        {
            var normalised = Normalised(group);
            EnsureValid(normalised, groups);
            groups.Add(normalised);
        }

        if (!groups.Any(group => group.IsGlobal))
        {
            throw new MappingValidationException("The Global group is missing.");
        }

        EnsureCommandIdsUnique(groups);

        var ignored = new List<IgnoredApp>();
        foreach (var app in document.Ignored)
        {
            var normalised = Normalised(app);
            EnsureValid(normalised, ignored);
            ignored.Add(normalised);
        }

        var perCommand = ignored.Where(app => app.IsPerCommand).Select(app => app.Id).ToHashSet();
        var plainGlobal = ignored.Where(app => !app.IsPerCommand && !app.DisableEntirely).Select(app => app.Id).ToHashSet();
        groups = [.. groups.Select(group => WithKnownAlsoIn(WithKnownNotIn(group, perCommand), plainGlobal))];

        var sortedGroups = groups
            .OrderBy(group => group.IsGlobal ? 0 : 1)
            .ThenBy(group => group.Name, NameComparer)
            .ToArray();
        return new MappingDocument(sortedGroups, ignored.ToArray());
    }

    /// <summary>
    /// Trims the name, normalises and sorts the commands, categories and hold remaps (a command in a category the group lacks
    /// becomes Uncategorized, one under a hold remap it lacks ordinary); Global never has a matcher, suppresses or stays off a platform.
    /// </summary>
    public static AppGroup Normalised(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var commands = group.Commands.Select(Normalised).OrderBy(command => command.Name, NameComparer).ToArray();
        return HoldRemapRules.Normalised(CategoryRules.Normalised(group.IsGlobal
            ? group with { Name = Trimmed(group.Name), Matcher = null, SuppressGlobals = false, UseOn = PlatformSet.All, Commands = commands }
            : group with { Name = Trimmed(group.Name), Commands = commands }));
    }

    /// <summary>Trims the name and normalises the trigger and an own version's trigger (<see cref="Trigger.Normalised"/>: a gesture or click holds the stroke button, a click holding nothing else is no trigger).</summary>
    public static Command Normalised(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var own = command.OwnVersion is { Trigger: { } trigger } version ? version with { Trigger = trigger.Normalised() } : command.OwnVersion;
        return command with { Name = Trimmed(command.Name), Trigger = command.Trigger.Normalised(), OwnVersion = own };
    }

    /// <summary>
    /// The group with its commands' "Not in" lists naming only Per command entries in <paramref name="perCommand"/>, once each,
    /// sorted by id, and empty under a hold remap (its hold remap plays it, never the resolver; plan 0004).
    /// </summary>
    private static AppGroup WithKnownNotIn(AppGroup group, HashSet<GroupId> perCommand)
    {
        if (group.Commands.All(command => command.NotIn.Count == 0))
        {
            return group;
        }

        return group with
        {
            Commands = [.. group.Commands.Select(command => command.NotIn.Count == 0 ? command : command with
            {
                NotIn = command.HoldRemapId is not null ? [] : [.. command.NotIn.Where(perCommand.Contains).Distinct().OrderBy(id => id.Value)],
            })],
        };
    }

    /// <summary>
    /// The group with its commands' "Also in" lists (plan 0005 decision 7) naming only Exclusions › Global entries without the
    /// disable-while-focused mode in <paramref name="plainGlobal"/>, once each, sorted by id; empty under a hold remap and on a
    /// command whose trigger holds the stroke button on both platforms (nothing that uses it is allowed back into an excluded app).
    /// </summary>
    private static AppGroup WithKnownAlsoIn(AppGroup group, HashSet<GroupId> plainGlobal)
    {
        if (group.Commands.All(command => command.AlsoIn.Count == 0))
        {
            return group;
        }

        return group with
        {
            Commands = [.. group.Commands.Select(command => command.AlsoIn.Count == 0 ? command : command with
            {
                AlsoIn = !CanWorkOverExcluded(command)
                    ? []
                    : [.. command.AlsoIn.Where(plainGlobal.Contains).Distinct().OrderBy(id => id.Value)],
            })],
        };
    }

    /// <summary>
    /// True when the command may keep an "Also in" (plan 0005 decision 7): not under a hold remap, and its trigger holds a button
    /// other than the stroke button, without it, on Windows or on macOS. The Also in dialog's row and an excluded app's Allowed
    /// for list offer exactly these commands.
    /// </summary>
    public static bool CanWorkOverExcluded(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.HoldRemapId is null
            && (command.TriggerFor(HostPlatform.Windows) is { IsBound: true, Hold.HandsBackDrags: true }
                || command.TriggerFor(HostPlatform.MacOS) is { IsBound: true, Hold.HandsBackDrags: true });
    }

    /// <summary>Trims the name; a Per command entry never disables Augram while focused (it stops only the commands that name it).</summary>
    public static IgnoredApp Normalised(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app with { Name = Trimmed(app.Name), DisableEntirely = app.DisableEntirely && !app.IsPerCommand };
    }

    /// <summary>Checks a normalised group, its categories and each of its commands against the groups it will sit beside.</summary>
    public static void EnsureValid(AppGroup group, IEnumerable<AppGroup> others)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(others);

        if (group.Name.Length == 0)
        {
            throw new MappingValidationException("An app group needs a name.");
        }

        foreach (var other in others)
        {
            if (other.Id == group.Id)
            {
                throw new MappingValidationException(group.IsGlobal
                    ? "Only one Global group is allowed."
                    : $"An app group with id {group.Id} already exists.");
            }

            if (NameComparer.Equals(other.Name, group.Name))
            {
                throw new MappingValidationException($"An app group named '{other.Name}' already exists.");
            }
        }

        if (group.UseOn == PlatformSet.None)
        {
            throw new MappingValidationException($"Use '{group.Name}' on at least one platform.");
        }

        if (group.Matcher is not null)
        {
            EnsureValid(group.Matcher);
        }

        CategoryRules.EnsureValid(group);
        HoldRemapRules.EnsureValid(group);
        var commands = new List<Command>();
        foreach (var command in group.Commands)
        {
            EnsureValid(command, group, commands);
            commands.Add(command);
        }
    }

    /// <summary>
    /// Checks a normalised command against the other commands of its group: ids unique; a name unique among its siblings (the
    /// commands of its parent, <see cref="CommandNames"/>: its hold remap's, or the group's ordinary commands); A7 among the same
    /// siblings (one command per trigger, an input once per hold remap, where two triggers are the same when they overlap on
    /// either platform, learnings 0003 §3.5); that every trigger it has can be held (a wheel trigger needs a button to hold);
    /// and <see cref="HoldRemapRules.EnsureValid(Command, AppGroup)"/>.
    /// </summary>
    public static void EnsureValid(Command command, AppGroup group, IEnumerable<Command> others)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(others);

        if (command.Name.Length == 0)
        {
            throw new MappingValidationException("A command needs a name.");
        }

        if (command.UseOn == PlatformSet.None)
        {
            throw new MappingValidationException($"Use '{command.Name}' on at least one platform.");
        }

        EnsureHoldable(command, command.Trigger);
        if (command.OwnVersion?.Trigger is { } own)
        {
            EnsureHoldable(command, own);
        }

        HoldRemapRules.EnsureValid(command, group);

        var mine = TriggersOf(command);
        foreach (var other in others)
        {
            if (other.Id == command.Id)
            {
                throw new MappingValidationException($"A command with id {command.Id} already exists.");
            }

            if (!CommandNames.AreSiblings(command, other))
            {
                continue;
            }

            if (NameComparer.Equals(other.Name, command.Name))
            {
                throw new MappingValidationException($"A command named '{other.Name}' already exists {CommandNames.Where(group, command)}.");
            }

            if (OverlapOf(mine, other) is { } clash)
            {
                throw new MappingValidationException($"'{other.Name}' {CommandNames.Where(group, command)} already uses {clash}.");
            }
        }
    }

    /// <summary>
    /// A7 between two commands: the phrase of the trigger they share on some platform ("Shift + this gesture", with " on macOS"
    /// when they share it on one platform only), or null when they share none. Commands under different hold remaps (or one
    /// under a hold remap and one not) share none: an input is unique per hold remap (F9, plan 0002).
    /// </summary>
    public static string? Overlap(Command command, Command other)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(other);
        return CommandNames.AreSiblings(command, other) ? OverlapOf(TriggersOf(command), other) : null;
    }

    private static (Trigger Windows, Trigger MacOS) TriggersOf(Command command)
        => (command.TriggerFor(HostPlatform.Windows), command.TriggerFor(HostPlatform.MacOS));

    private static string? OverlapOf((Trigger Windows, Trigger MacOS) mine, Command other)
    {
        var onWindows = mine.Windows.Overlaps(other.TriggerFor(HostPlatform.Windows));
        var onMac = mine.MacOS.Overlaps(other.TriggerFor(HostPlatform.MacOS));
        return (onWindows, onMac) switch
        {
            (true, true) => mine.Windows.Describe(),
            (true, false) => $"{mine.Windows.Describe(HostPlatform.Windows)} on Windows",
            (false, true) => $"{mine.MacOS.Describe(HostPlatform.MacOS)} on macOS",
            _ => null,
        };
    }

    private static void EnsureHoldable(Command command, Trigger trigger)
    {
        if (trigger is Trigger.ButtonTrigger button)
        {
            // Plan 0005 decision 2: held back by another button, never by the stroke button (whose presses draw or click).
            if (trigger.Hold.HoldsStroke)
            {
                throw new MappingValidationException($"'{command.Name}' holds the stroke button: a button trigger holds another button, as Right in Right + {button.Button}.");
            }

            if (trigger.Hold.Physical == HeldButtons.None)
            {
                throw new MappingValidationException($"'{command.Name}' holds no button: a button trigger needs a button to hold, as Right in Right + {button.Button}.");
            }
        }

        if (trigger.IsBound && !trigger.Hold.HasAnchor)
        {
            throw new MappingValidationException($"'{command.Name}' holds no button: a wheel trigger needs the stroke button or another button to hold.");
        }

        if (trigger.Hold.DragDistancePx is { } distance && distance is < 1 or > TriggerHold.MaxDragDistancePx)
        {
            throw new MappingValidationException($"The drag distance of '{command.Name}' must be between 1 and {TriggerHold.MaxDragDistancePx} px.");
        }
    }

    public static void EnsureValid(IgnoredApp app, IEnumerable<IgnoredApp> others)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(others);

        if (app.Name.Length == 0)
        {
            throw new MappingValidationException("An excluded app needs a name.");
        }

        foreach (var other in others)
        {
            if (other.Id == app.Id)
            {
                throw new MappingValidationException($"An excluded app with id {app.Id} already exists.");
            }

            // Unique like group names (Joel, 2026-10-09).
            if (NameComparer.Equals(other.Name, app.Name))
            {
                throw new MappingValidationException($"An excluded app named '{other.Name}' already exists.");
            }
        }

        EnsureValid(app.Matcher);
    }

    /// <summary>A regex field that is switched on must hold a pattern the regex engine accepts.</summary>
    public static void EnsureValid(AppMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        foreach (var name in matcher.WindowsProcessNames)
        {
            EnsurePattern("executable name", name, matcher.WindowsProcessNamesAreRegex);
        }

        foreach (var name in matcher.MacProcessNames)
        {
            EnsurePattern("macOS executable name", name, matcher.MacProcessNamesAreRegex);
        }

        EnsurePattern("path", matcher.ProcessPath, matcher.ProcessPathIsRegex);
        EnsurePattern("macOS path", matcher.MacProcessPath, matcher.MacProcessPathIsRegex);
        EnsurePattern("title", matcher.Title, matcher.TitleIsRegex);
        EnsurePattern("macOS title", matcher.MacTitle, matcher.MacTitleIsRegex);
        foreach (var (name, pattern, isRegex, _) in matcher.WindowFields)
        {
            EnsurePattern(name, pattern, isRegex);
        }
    }

    /// <summary>The regex engine's complaint about <paramref name="pattern"/>, or null when it is valid (the importer checks each field it reads).</summary>
    public static string? PatternProblem(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return MatcherRegexCache.Problem(pattern);
    }

    private static void EnsurePattern(string field, string? pattern, bool isRegex)
    {
        if (!isRegex || string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        var problem = MatcherRegexCache.Problem(pattern);
        if (problem is not null)
        {
            throw new MappingValidationException($"The {field} pattern '{pattern}' is not a valid regular expression: {problem}");
        }
    }

    private static void EnsureCommandIdsUnique(List<AppGroup> groups)
    {
        var seen = new HashSet<CommandId>();
        foreach (var group in groups)
        {
            foreach (var command in group.Commands)
            {
                if (!seen.Add(command.Id))
                {
                    throw new MappingValidationException($"A command with id {command.Id} exists in more than one group.");
                }
            }
        }
    }

    private static string Trimmed(string? name) => name?.Trim() ?? string.Empty;
}
