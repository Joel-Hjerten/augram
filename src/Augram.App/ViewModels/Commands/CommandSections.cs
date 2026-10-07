using Augram.App.Components.CommandTree;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// How each Commands sub-tab cuts the mapping into sections (Joel, 2026-10-07): the Apps tab has one
/// section per app group, Global left out, and tags the rows of a group that has categories (Photoshop)
/// with the category's name; the Global tab has the Global group's categories by name, after an
/// Uncategorized section that is there only while some Global command has no category (or names one the
/// group no longer has). The one place that decides which commands a tab holds, which section a command
/// sits in, what a section header can do and what the header's Category dropdown offers.
/// </summary>
internal static class CommandSections
{
    /// <summary>The Global tab holds the Global group; the Apps tab every other group.</summary>
    public static bool Includes(CommandsScope scope, AppGroup group) => group.IsGlobal == (scope == CommandsScope.Global);

    public static IReadOnlyList<SectionItem> For(CommandsScope scope, MappingDocument document, IReadOnlySet<SectionId> expanded, Func<GestureId, Gesture?> findGesture)
    {
        ArgumentNullException.ThrowIfNull(document);
        return scope == CommandsScope.Global
            ? GlobalSections(document.Global, expanded, findGesture)
            : [.. document.Groups.Where(group => !group.IsGlobal).Select(group => GroupSection(group, expanded, findGesture))];
    }

    public static SectionId SectionOf(CommandsScope scope, AppGroup group, Command command)
        => scope == CommandsScope.Apps ? SectionId.ForGroup(group.Id)
            : command.CategoryId is { } id && group.FindCategory(id) is not null ? SectionId.ForCategory(group.Id, id)
            : SectionId.Uncategorized;

    /// <summary>Uncategorized, then the group's categories by name; none for an app group without categories (the dropdown hides).</summary>
    public static IReadOnlyList<CategoryChoice> Choices(AppGroup group)
        => group.IsGlobal || group.Categories.Count > 0
            ? [CategoryChoice.Uncategorized, .. Sorted(group).Select(category => new CategoryChoice(category.Id, category.Name))]
            : [];

    private static SectionItem GroupSection(AppGroup group, IReadOnlySet<SectionId> expanded, Func<GestureId, Gesture?> findGesture)
    {
        var id = SectionId.ForGroup(group.Id);
        var choices = Choices(group);
        var commands = group.Commands
            .Select(command => Item(group, command, findGesture) with
            {
                Section = id,
                CategoryLabel = command.CategoryId is { } category ? group.FindCategory(category)?.Name : null,
                Categories = choices,
            })
            .ToList();
        return new SectionItem(id, group.Name, group.IsActive, expanded.Contains(id), commands)
        {
            CanRename = true,
            CanDelete = true,
            CanEditDefinition = true,
            CanToggleActive = true,
        };
    }

    private static List<SectionItem> GlobalSections(AppGroup global, IReadOnlySet<SectionId> expanded, Func<GestureId, Gesture?> findGesture)
    {
        var choices = Choices(global);
        var items = global.Commands
            .Select(command => Item(global, command, findGesture) with { Section = SectionOf(CommandsScope.Global, global, command), Categories = choices })
            .ToList();
        var sections = new List<SectionItem>();
        var uncategorized = items.Where(item => item.Section == SectionId.Uncategorized).ToList();
        if (uncategorized.Count > 0)
        {
            sections.Add(new SectionItem(SectionId.Uncategorized, CategoryChoice.UncategorizedName, global.IsActive, expanded.Contains(SectionId.Uncategorized), uncategorized));
        }

        foreach (var category in Sorted(global))
        {
            var id = SectionId.ForCategory(global.Id, category.Id);
            sections.Add(new SectionItem(id, category.Name, global.IsActive, expanded.Contains(id), [.. items.Where(item => item.Section == id)])
            {
                CanRename = true,
                CanDelete = true,
            });
        }

        return sections;
    }

    private static IEnumerable<CommandCategory> Sorted(AppGroup group) => group.Categories.OrderBy(category => category.Name, MappingRules.NameComparer);

    private static CommandItem Item(AppGroup group, Command command, Func<GestureId, Gesture?> findGesture)
        => CommandItem.From(group, command, command.Trigger is Trigger.GestureTrigger gesture ? findGesture(gesture.GestureId) : null);
}
