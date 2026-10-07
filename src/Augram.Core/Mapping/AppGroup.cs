namespace Augram.Core.Mapping;

/// <summary>
/// One group of commands (F5a): the Global group, or one app identified by its <see cref="Matcher"/>.
/// <see cref="SuppressGlobals"/> is SP.net's "No Global Actions": over this app only its own commands
/// fire. Commands are kept sorted by name (F5a: never hand-ordered). The Global group has no matcher
/// and cannot be removed; <see cref="MappingRules"/> keeps both true. <paramref name="Categories"/> are the
/// group's command sections (<see cref="CommandCategory"/>), sorted by name; empty for most app groups.
/// </summary>
public sealed record AppGroup(
    GroupId Id,
    string Name,
    bool IsActive,
    bool SuppressGlobals,
    AppMatcher? Matcher,
    IReadOnlyList<Command> Commands,
    IReadOnlyList<CommandCategory>? Categories = null)
{
    public IReadOnlyList<CommandCategory> Categories { get; init; } = Categories ?? [];

    /// <summary>Where the group takes part (F8 "Use on", Joel 2026-10-07); both by default. Not consulted for the Global group, which is everywhere.</summary>
    public PlatformSet UseOn { get; init; } = PlatformSet.All;

    /// <summary>True for the Global group, and for an app group used on <paramref name="platform"/>.</summary>
    public bool IsUsedOn(Abstractions.HostPlatform platform) => IsGlobal || UseOn.Includes(platform);

    /// <summary>
    /// The "Use on" rule for a command (F8, Joel 2026-10-08), the one place it is decided: a command is used on a platform
    /// only if its group, its category (when it has one) and the command itself all include it. The resolver, the lists and
    /// the command header all ask this; none of the three stored values is changed by the others.
    /// </summary>
    public bool IsCommandUsedOn(Command command, Abstractions.HostPlatform platform) => EffectiveUseOn(command).Includes(platform);

    /// <summary>Where <paramref name="command"/> takes part: <see cref="UseOnLimitFor"/> ANDed with the command's own <see cref="Command.UseOn"/>.</summary>
    public PlatformSet EffectiveUseOn(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return UseOnLimitFor(command) & command.UseOn;
    }

    /// <summary>
    /// The platforms the group and the command's category leave the command: the group's (every platform for Global) ANDed
    /// with its category's (every platform for Uncategorized). A platform outside it is set by the group or the category,
    /// whatever the command's own value says.
    /// </summary>
    public PlatformSet UseOnLimitFor(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return (IsGlobal ? PlatformSet.All : UseOn) & (CategoryOf(command)?.UseOn ?? PlatformSet.All);
    }

    /// <summary>The category <paramref name="command"/> is sorted into; null for Uncategorized, or for a category this group does not have.</summary>
    public CommandCategory? CategoryOf(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.CategoryId is { } id ? FindCategory(id) : null;
    }

    public const string GlobalName = "Global";

    /// <summary>The Global group of a fresh document: active, no commands.</summary>
    public static AppGroup EmptyGlobal { get; } = new(GroupId.Global, GlobalName, IsActive: true, SuppressGlobals: false, Matcher: null, Commands: []);

    public bool IsGlobal => Id == GroupId.Global;

    public CommandCategory? FindCategory(CategoryId id) => Categories.FirstOrDefault(category => category.Id == id);

    public Command? FindCommand(CommandId id)
    {
        foreach (var command in Commands)
        {
            if (command.Id == id)
            {
                return command;
            }
        }

        return null;
    }
}
