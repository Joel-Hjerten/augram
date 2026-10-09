using Augram.Core.Abstractions;
using Augram.Core.Gestures;

namespace Augram.Core.Mapping;

/// <summary>
/// The business rules of the mapping (ADR-0002 §5a: one home, called by the store; view models only
/// display the outcome). Names compare trimmed and case-insensitively, like gesture names. Group,
/// command and category order is not a user choice (F5a): <see cref="ValidDocument"/> sorts groups Global
/// first and then by name, and commands and categories by name, so every snapshot the store hands out is
/// already in display order. A group's categories follow <see cref="CategoryRules"/>.
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

        var sortedGroups = groups
            .OrderBy(group => group.IsGlobal ? 0 : 1)
            .ThenBy(group => group.Name, NameComparer)
            .ToArray();
        return new MappingDocument(sortedGroups, ignored.ToArray());
    }

    /// <summary>
    /// Trims the name, normalises and sorts the commands and the categories (a command in a category the
    /// group lacks becomes Uncategorized); the Global group never has a matcher, suppresses itself or stays off a platform.
    /// </summary>
    public static AppGroup Normalised(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var commands = group.Commands.Select(Normalised).OrderBy(command => command.Name, NameComparer).ToArray();
        return CategoryRules.Normalised(group.IsGlobal
            ? group with { Name = Trimmed(group.Name), Matcher = null, SuppressGlobals = false, UseOn = PlatformSet.All, Commands = commands }
            : group with { Name = Trimmed(group.Name), Commands = commands });
    }

    /// <summary>Trims the name and normalises the trigger and an own version's trigger (<see cref="Trigger.Normalised"/>: a gesture or click holds the stroke button, a click holding nothing else is no trigger).</summary>
    public static Command Normalised(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var own = command.OwnVersion is { Trigger: { } trigger } version ? version with { Trigger = trigger.Normalised() } : command.OwnVersion;
        return command with { Name = Trimmed(command.Name), Trigger = command.Trigger.Normalised(), OwnVersion = own };
    }

    public static IgnoredApp Normalised(IgnoredApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app with { Name = Trimmed(app.Name) };
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
        var commands = new List<Command>();
        foreach (var command in group.Commands)
        {
            EnsureValid(command, group, commands);
            commands.Add(command);
        }
    }

    /// <summary>
    /// Checks a normalised command against the other commands of its group (A7: one command per trigger per group, where two
    /// triggers are the same when they overlap on either platform, learnings 0003 §3.5), and that every trigger it has can be
    /// held: a wheel trigger without the stroke button holds another button.
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

        var mine = TriggersOf(command);
        foreach (var other in others)
        {
            if (other.Id == command.Id)
            {
                throw new MappingValidationException($"A command with id {command.Id} already exists.");
            }

            if (NameComparer.Equals(other.Name, command.Name))
            {
                throw new MappingValidationException($"A command named '{other.Name}' already exists in '{group.Name}'.");
            }

            if (OverlapOf(mine, other) is { } clash)
            {
                throw new MappingValidationException($"'{other.Name}' in '{group.Name}' already uses {clash}.");
            }
        }
    }

    /// <summary>
    /// A7 between two commands: the phrase of the trigger they share on some platform ("Shift + this gesture", with " on macOS"
    /// when they share it on one platform only), or null when they share none.
    /// </summary>
    public static string? Overlap(Command command, Command other)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(other);
        return OverlapOf(TriggersOf(command), other);
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
        if (trigger.IsBound && !trigger.Hold.HasAnchor)
        {
            throw new MappingValidationException($"'{command.Name}' holds no button: a wheel trigger needs the stroke button or another button to hold.");
        }
    }

    public static void EnsureValid(IgnoredApp app, IEnumerable<IgnoredApp> others)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(others);

        if (app.Name.Length == 0)
        {
            throw new MappingValidationException("An ignored app needs a name.");
        }

        foreach (var other in others)
        {
            if (other.Id == app.Id)
            {
                throw new MappingValidationException($"An ignored app with id {app.Id} already exists.");
            }

            // Unique like group names (Joel, 2026-10-09).
            if (NameComparer.Equals(other.Name, app.Name))
            {
                throw new MappingValidationException($"An ignored app named '{other.Name}' already exists.");
            }
        }

        EnsureValid(app.Matcher);
    }

    /// <summary>A regex field that is switched on must hold a pattern the regex engine accepts.</summary>
    public static void EnsureValid(AppMatcher matcher)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        EnsurePattern("path", matcher.ProcessPath, matcher.ProcessPathIsRegex);
        EnsurePattern("title", matcher.Title, matcher.TitleIsRegex);
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
