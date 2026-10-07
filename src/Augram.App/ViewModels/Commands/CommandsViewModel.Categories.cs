using Augram.App.Components.CommandTree;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The category half of <see cref="CommandsViewModel"/> (the Global tab's sections, Joel 2026-10-07;
/// SP.net's Global categories): "New category N" renamed in place, rename, and delete with confirmation,
/// whose commands move to Uncategorized in the same undo step. Each is one
/// <see cref="MappingStore.UpdateGroup"/> of the Global group; the rules (names unique within the group)
/// answer through their message.
/// </summary>
public sealed partial class CommandsViewModel
{
    /// <summary>Adds "New category N" to the Global group, opens and selects its section, and asks the tree to rename it in place.</summary>
    private void NewCategory()
    {
        var global = _store.Global;
        var category = new CommandCategory(CategoryId.New(), FreeNames.Next("New category", global.Categories.Select(existing => existing.Name)));
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

    private async Task DeleteCategoryAsync(CategoryId id)
    {
        var global = _store.Global;
        if (global.FindCategory(id) is not { } category)
        {
            Message = "That category no longer exists.";
            return;
        }

        var count = global.Commands.Count(command => command.CategoryId == id);
        var question = count switch
        {
            0 => $"Delete category '{category.Name}'? It has no commands.",
            1 => $"Delete category '{category.Name}'? Its command moves to Uncategorized.",
            _ => $"Delete category '{category.Name}'? Its {count} commands move to Uncategorized.",
        };
        if (!await _confirm.ConfirmAsync("Delete category", question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var current = _store.Global;
            _store.UpdateGroup(current with
            {
                Categories = [.. current.Categories.Where(existing => existing.Id != id)],
                Commands = [.. current.Commands.Select(command => command.CategoryId == id ? command with { CategoryId = null } : command)],
            });
            Message = $"Deleted '{category.Name}'. {CommandsKeymap.Current.Undo} undoes it.";
        });
    }
}
