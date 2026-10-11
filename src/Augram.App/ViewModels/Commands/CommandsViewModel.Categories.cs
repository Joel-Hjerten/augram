using Augram.App.Components.CommandTree;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The category half of <see cref="CommandsViewModel"/> (the Global tab's sections, Joel 2026-10-07;
/// SP.net's Global categories): "New category N" renamed in place, rename, and delete with confirmation,
/// whose commands move to Uncategorized in the same undo step; deleting Uncategorized deletes its commands (Joel,
/// 2026-10-11: it would not go, unlike every other section). Each is one
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

    /// <summary>
    /// Asks, then deletes the commands Uncategorized shows (those used here, or all with Show other platforms on) in one
    /// <see cref="MappingStore.UpdateGroup"/>, so one undo brings them all back; the section goes with its last command, as when
    /// it is emptied any other way. A command already gone meanwhile is skipped.
    /// </summary>
    private async Task DeleteUncategorizedAsync()
    {
        var commands = Sections.FirstOrDefault(section => section.Id == SectionId.Uncategorized)?.Commands ?? [];
        if (commands.Count == 0)
        {
            Message = "Uncategorized has no commands.";
            return;
        }

        var (title, question) = commands.Count == 1
            ? ("Delete command", $"Delete the uncategorized command '{commands[0].Name}'?")
            : ("Delete commands", $"Delete the {commands.Count} uncategorized commands?");
        if (!await _confirm.ConfirmAsync(title, question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        var ids = commands.Select(command => command.Id).ToHashSet();
        Guard(() =>
        {
            var current = _store.Global;
            var removed = current.Commands.Where(command => ids.Contains(command.Id)).ToList();
            if (removed.Count == 0)
            {
                Message = "Those commands no longer exist.";
                return;
            }

            _store.UpdateGroup(current with { Commands = [.. current.Commands.Where(command => !ids.Contains(command.Id))] });
            Message = removed.Count == 1
                ? $"Deleted '{removed[0].Name}'. {CommandsKeymap.Current.Undo} undoes it."
                : $"Deleted {removed.Count} uncategorized commands. {CommandsKeymap.Current.Undo} undoes it.";
        });
    }
}
