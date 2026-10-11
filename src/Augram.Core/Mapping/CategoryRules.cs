using System.Globalization;

namespace Augram.Core.Mapping;

/// <summary>
/// The rules for a group's <see cref="CommandCategory"/> list, split out of <see cref="MappingRules"/>,
/// which runs them for every group it normalises and validates (so every store commit, load and import
/// goes through them). Names are trimmed, must not be empty and are unique within the group
/// case-insensitively (<see cref="MappingRules.NameComparer"/>); ids are unique within the group; a category is used on
/// at least one platform (F8 "Use on", as a group and a command must be);
/// categories are sorted by name like groups and commands. A command whose
/// <see cref="Command.CategoryId"/> names no category of its group is normalised to none, never refused: a hand-edited
/// file, a deleted category or a moved command must not fail. **On Global every command is in a category** (Joel,
/// 2026-10-11): one left without goes into the category named <see cref="UncategorizedName"/>, made when the group has
/// none; an app group's command may have none ("No category" in the App).
/// </summary>
public static class CategoryRules
{
    /// <summary>The name of the Global category a command goes into when it has no other (Joel, 2026-10-11).</summary>
    public const string UncategorizedName = "Uncategorized";

    /// <summary>
    /// The id the first "Uncategorized" category is made with. A fixed value, the same on every install, so normalising stays a
    /// pure function and two machines that each make one end with one category (sync merges categories by id, and the same
    /// id with the same content is one item). When the group already uses it (the user renamed that category), the next one
    /// takes the next fixed id (<c>…c00000000002</c>, and so on), again the same everywhere.
    /// </summary>
    public static CategoryId UncategorizedId { get; } = UncategorizedCandidate(1);

    /// <summary>
    /// Trims and sorts the group's categories and clears every command's category that is not among them; on Global, puts every
    /// command without a category into <see cref="UncategorizedName"/>, adding that category when the group has none.
    /// </summary>
    public static AppGroup Normalised(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var categories = group.Categories
            .Select(category => category with { Name = category.Name?.Trim() ?? string.Empty })
            .ToList();
        var ids = categories.Select(category => category.Id).ToHashSet();
        var commands = group.Commands
            .Select(command => command.CategoryId is { } id && !ids.Contains(id) ? command with { CategoryId = null } : command)
            .ToArray();
        if (group.IsGlobal && commands.Any(command => command.CategoryId is null))
        {
            var home = FindByName(categories, UncategorizedName);
            if (home is null)
            {
                home = new CommandCategory(FreeUncategorizedId(ids), UncategorizedName);
                categories.Add(home);
            }

            commands = [.. commands.Select(command => command.CategoryId is null ? command with { CategoryId = home.Id } : command)];
        }

        return group with { Categories = [.. categories.OrderBy(category => category.Name, MappingRules.NameComparer)], Commands = commands };
    }

    private static CategoryId FreeUncategorizedId(HashSet<CategoryId> taken)
    {
        var index = 1L;
        while (taken.Contains(UncategorizedCandidate(index)))
        {
            index++;
        }

        return UncategorizedCandidate(index);
    }

    private static CategoryId UncategorizedCandidate(long index)
        => new(new Guid("00000000-0000-4000-8000-c" + index.ToString("x11", CultureInfo.InvariantCulture)));

    /// <summary>Checks a normalised group's categories among themselves.</summary>
    public static void EnsureValid(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var accepted = new List<CommandCategory>();
        foreach (var category in group.Categories)
        {
            EnsureValid(category, group, accepted);
            accepted.Add(category);
        }
    }

    /// <summary>Checks a trimmed category against the other categories of its group (what an Add or Rename asks before it commits).</summary>
    public static void EnsureValid(CommandCategory category, AppGroup group, IEnumerable<CommandCategory> others)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(others);

        if (string.IsNullOrEmpty(category.Name))
        {
            throw new MappingValidationException($"A category in '{group.Name}' needs a name.");
        }

        if (category.UseOn == PlatformSet.None)
        {
            throw new MappingValidationException($"Use '{category.Name}' on at least one platform.");
        }

        Unique.Ensure(
            others,
            other => other.Id == category.Id,
            other => other.Name,
            category.Name,
            $"A category with id {category.Id} already exists in '{group.Name}'.",
            taken => $"A category named '{taken}' already exists in '{group.Name}'.");
    }

    /// <summary>The group's category with this name, compared trimmed and case-insensitively; null when it has none.</summary>
    internal static CommandCategory? FindByName(AppGroup group, string name)
    {
        ArgumentNullException.ThrowIfNull(group);
        return FindByName(group.Categories, name);
    }

    /// <summary>
    /// The category a Global command without one goes into (<see cref="UncategorizedName"/>); null while the group has none. A
    /// renamed one is an ordinary category. Deleting this one deletes its commands, since they have nowhere else to go;
    /// deleting any other category moves its commands here (the App asks first; Joel, 2026-10-11).
    /// </summary>
    public static CommandCategory? Uncategorized(AppGroup group) => FindByName(group, UncategorizedName);

    private static CommandCategory? FindByName(IEnumerable<CommandCategory> categories, string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return categories.FirstOrDefault(category => MappingRules.NameComparer.Equals(category.Name, trimmed));
    }

    /// <summary>
    /// The category a command takes when it goes from <paramref name="from"/> to <paramref name="to"/>
    /// (a move, a merge): the category of <paramref name="to"/> named like its category in
    /// <paramref name="from"/>, or null when it had none or the target has no such name (which <see cref="Normalised"/> makes
    /// Uncategorized on Global).
    /// </summary>
    public static CategoryId? Carried(CategoryId? id, AppGroup from, AppGroup to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (id is not { } value || from.FindCategory(value) is not { } category)
        {
            return null;
        }

        return FindByName(to, category.Name)?.Id;
    }
}
