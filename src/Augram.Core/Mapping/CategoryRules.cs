namespace Augram.Core.Mapping;

/// <summary>
/// The rules for a group's <see cref="CommandCategory"/> list, split out of <see cref="MappingRules"/>,
/// which runs them for every group it normalises and validates (so every store commit, load and import
/// goes through them). Names are trimmed, must not be empty and are unique within the group
/// case-insensitively (<see cref="MappingRules.NameComparer"/>); ids are unique within the group; a category is used on
/// at least one platform (F8 "Use on", as a group and a command must be);
/// categories are sorted by name like groups and commands. A command whose
/// <see cref="Command.CategoryId"/> names no category of its group is normalised to Uncategorized
/// (null), never refused: a hand-edited file, a deleted category or a moved command must not fail.
/// </summary>
public static class CategoryRules
{
    /// <summary>Trims and sorts the group's categories and clears every command's category that is not among them.</summary>
    public static AppGroup Normalised(AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var categories = group.Categories
            .Select(category => category with { Name = category.Name?.Trim() ?? string.Empty })
            .OrderBy(category => category.Name, MappingRules.NameComparer)
            .ToArray();
        var ids = categories.Select(category => category.Id).ToHashSet();
        var commands = group.Commands
            .Select(command => command.CategoryId is { } id && !ids.Contains(id) ? command with { CategoryId = null } : command)
            .ToArray();
        return group with { Categories = categories, Commands = commands };
    }

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

        foreach (var other in others)
        {
            if (other.Id == category.Id)
            {
                throw new MappingValidationException($"A category with id {category.Id} already exists in '{group.Name}'.");
            }

            if (MappingRules.NameComparer.Equals(other.Name, category.Name))
            {
                throw new MappingValidationException($"A category named '{other.Name}' already exists in '{group.Name}'.");
            }
        }
    }

    /// <summary>The group's category with this name, compared trimmed and case-insensitively; null when it has none.</summary>
    public static CommandCategory? FindByName(AppGroup group, string name)
    {
        ArgumentNullException.ThrowIfNull(group);
        var trimmed = name?.Trim() ?? string.Empty;
        return group.Categories.FirstOrDefault(category => MappingRules.NameComparer.Equals(category.Name, trimmed));
    }

    /// <summary>
    /// The category a command takes when it goes from <paramref name="from"/> to <paramref name="to"/>
    /// (a move, a merge): the category of <paramref name="to"/> named like its category in
    /// <paramref name="from"/>, or null (Uncategorized) when it had none or the target has no such name.
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
