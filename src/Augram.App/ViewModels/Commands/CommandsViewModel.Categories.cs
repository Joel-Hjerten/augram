using Augram.App.Components.CommandTree;
using Augram.App.Components.ListEditing;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The category half of <see cref="CommandsViewModel"/> (the Global tab's sections, Joel 2026-10-07;
/// SP.net's Global categories): "New category N" renamed in place, rename, and delete with confirmation,
/// whose commands move to Uncategorized in the same undo step (the rules make it when it is missing); deleting Uncategorized
/// itself deletes its commands, which have nowhere else to go (Joel, 2026-10-11: Uncategorized is an ordinary category,
/// renamed and deleted like any other). Each is one <see cref="MappingStore.UpdateGroup"/> of the Global group; the rules
/// (names unique within the group) answer through their message.
/// </summary>
public sealed partial class CommandsViewModel
{
    /// <summary>Adds "New category N" to the Global group, opens and selects its section, and asks the tree to rename it in place.</summary>
    private void NewCategory()
    {
        var global = _store.Global;
        var category = new CommandCategory(CategoryId.New(), FreeNames.Next("New category", global.Categories.Select(existing => existing.Name), MappingRules.NameComparer));
        var section = SectionId.ForCategory(global.Id, category.Id);
        _expanded.Add(section);
        _store.UpdateGroup(global with { Categories = [.. global.Categories, category] });
        Select(section, null);
        ProjectSelection();
        SectionRenameRequested?.Invoke(this, section);
    }

    private void RenameCategory(CategoryId id, string name)
    {
        var global = _store.Global;
        if (global.FindCategory(id) is null)
        {
            throw new KeyNotFoundException("That category no longer exists.");
        }

        _store.UpdateGroup(global with
        {
            Categories = [.. global.Categories.Select(category => category.Id == id ? category with { Name = name } : category)],
        });
    }

    /// <summary>
    /// Asks, then removes the category in one <see cref="MappingStore.UpdateGroup"/>, so one undo brings it back: its commands
    /// (every one, those other platforms use included) move to Uncategorized, or, when it is Uncategorized
    /// (<see cref="CategoryRules.Uncategorized"/>), go with it.
    /// </summary>
    private async Task DeleteCategoryAsync(CategoryId id)
    {
        var global = _store.Global;
        if (global.FindCategory(id) is not { } category)
        {
            Message = "That category no longer exists.";
            return;
        }

        var count = global.Commands.Count(command => command.CategoryId == id);
        var takesCommands = CategoryRules.Uncategorized(global)?.Id == id;
        var question = (count, takesCommands) switch
        {
            (0, _) => $"Delete category '{category.Name}'? It has no commands.",
            (1, true) => $"Delete category '{category.Name}' and its command?",
            (_, true) => $"Delete category '{category.Name}' and its {count} commands?",
            (1, false) => $"Delete category '{category.Name}'? Its command moves to {CategoryRules.UncategorizedName}.",
            _ => $"Delete category '{category.Name}'? Its {count} commands move to {CategoryRules.UncategorizedName}.",
        };
        await DeleteAsync("Delete category", question, () =>
        {
            var current = _store.Global;
            var commands = takesCommands
                ? current.Commands.Where(command => command.CategoryId != id)
                : current.Commands.Select(command => command.CategoryId == id ? command with { CategoryId = null } : command);
            _store.UpdateGroup(current with
            {
                Categories = [.. current.Categories.Where(existing => existing.Id != id)],
                Commands = [.. commands],
            });
            return Deleted(category.Name, EditKeymap.Current.Undo);
        }).ConfigureAwait(true);
    }
}
