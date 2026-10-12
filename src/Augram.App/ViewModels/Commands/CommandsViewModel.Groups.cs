using Augram.App.Components.CommandTree;
using Augram.App.Dialogs;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>The app group half of <see cref="CommandsViewModel"/> (F5, F5a; the Apps tab's sections): the app group form for a new group, rename, and delete with confirmation (its hold remaps and their commands go with it); the selected group's form lives in <c>.GroupPanel</c>. The store refuses renaming or deleting the Global group, whose tab lists only its categories.</summary>
public sealed partial class CommandsViewModel
{
    private async Task NewGroupAsync()
    {
        var edit = new GroupEditViewModel();
        if (!await _dialogs.ShowAsync(edit).ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var stored = _store.AddGroup(edit.ToGroup(GroupId.New()));
            Select(SectionId.ForGroup(stored.Id), null);
            ProjectSelection();
        });
    }

    /// <summary>Renames an app group; the store refuses renaming Global (<see cref="MappingRules.EnsureNotRenamingGlobal"/>), whose tab never offers it.</summary>
    private void RenameGroup(GroupId id, string name) => _store.UpdateGroup(RequireGroup(id) with { Name = name });

    private async Task DeleteGroupAsync(SectionItem section)
    {
        // Its commands and those of its hold remaps, which go with it.
        var count = Sections.Where(listed => listed.Id.GroupId == section.Id.GroupId).Sum(listed => listed.Commands.Count);
        var question = count == 1 ? $"Delete group '{section.Name}' and its command?" : $"Delete group '{section.Name}' and its {count} commands?";
        await DeleteAsync("Delete app group", question, () => Deleted(_store.RemoveGroup(section.Id.GroupId).Name, CommandsKeymap.Current.Undo)).ConfigureAwait(true);
    }

    private AppGroup RequireGroup(GroupId id) => _store.FindGroup(id) ?? throw new KeyNotFoundException("That app group no longer exists.");
}
