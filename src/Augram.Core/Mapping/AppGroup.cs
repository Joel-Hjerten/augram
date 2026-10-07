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
