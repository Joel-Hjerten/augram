using System.Text.Json;
using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Turns an application's <c>Categories[]</c> and its actions' <c>Category</c> names into the group's
/// <see cref="CommandCategory"/> list (fresh ids) and each command's <see cref="Command.CategoryId"/>
/// (Joel, 2026-10-07: the Global tab is organised by them). Leaves SP.net's noise behind: a group whose
/// actions all sit in the default "General" category gets no categories at all, and a listed category
/// no imported action uses is left out without a warning. An action naming a category the list lacks
/// gets one created for it, reported once per group as Info. Names match trimmed and case-insensitively
/// and keep the list's spelling; an action without a category imports Uncategorized.
/// </summary>
internal static class CategoryReader
{
    /// <param name="group">The group as read, its commands in the order <see cref="ActionReader.ReadCommands"/> returned them.</param>
    /// <param name="application">The SP.net application (or <c>GlobalApplication</c>) the group was read from.</param>
    /// <param name="actionCategories">The category name of each command's action, same index; empty for none.</param>
    /// <param name="warnings">The import report.</param>
    public static AppGroup Categorised(AppGroup group, JsonElement application, IReadOnlyList<string> actionCategories, List<ImportWarning> warnings)
    {
        var used = actionCategories.Where(name => name.Length > 0).Distinct(MappingRules.NameComparer).ToList();
        if (used.Count == 0 || (used.Count == 1 && MappingRules.NameComparer.Equals(used[0], StrokesPlusJson.Application.DefaultCategory)))
        {
            return group;
        }

        var listed = Listed(application);
        var byName = new Dictionary<string, CommandCategory>(MappingRules.NameComparer);
        foreach (var name in used)
        {
            if (!listed.TryGetValue(name, out var spelling))
            {
                spelling = name;
                warnings.Add(new ImportWarning(ImportSeverity.Info, group.Name, $"Category '{name}' is not in the application's category list; created for its commands."));
            }

            byName[name] = new CommandCategory(CategoryId.New(), spelling);
        }

        var commands = group.Commands
            .Select((command, index) => actionCategories[index].Length == 0
                ? command
                : command with { CategoryId = byName[actionCategories[index]].Id })
            .ToArray();
        return group with { Categories = byName.Values.ToArray(), Commands = commands };
    }

    /// <summary>The application's <c>Categories[]</c>: trimmed, non-empty names keyed case-insensitively to their first spelling.</summary>
    private static Dictionary<string, string> Listed(JsonElement application)
    {
        var listed = new Dictionary<string, string>(MappingRules.NameComparer);
        if (!JsonRead.TryArray(application, StrokesPlusJson.Application.Categories, out var categories))
        {
            return listed;
        }

        foreach (var item in categories.EnumerateArray())
        {
            var name = item.ValueKind == JsonValueKind.String ? item.GetString()!.Trim() : string.Empty;
            if (name.Length > 0)
            {
                listed.TryAdd(name, name);
            }
        }

        return listed;
    }
}
