using Augram.App.Components.CommandTree;
using Augram.App.Components.ListEditing;
using Augram.App.Dialogs;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>The app group half of <see cref="CommandsViewModel"/> (F5, F5a; the Apps tab's sections): the app group form for a new group, rename, delete with confirmation (its hold remaps and their commands go with it), and duplicate (Joel, 2026-11-14: the group again under a free name, its commands and hold remaps with it, without an executable); the selected group's form lives in <see cref="SectionPanel"/>. The store refuses renaming or deleting the Global group, whose tab lists only its categories.</summary>
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
    private void RenameGroup(GroupId id, string name) => _store.UpdateGroup(_store.RequireGroup(id) with { Name = name });

    /// <summary>
    /// A second group with the same commands, categories and hold remaps (<see cref="AppGroup.Duplicated"/>: fresh ids, the
    /// executable names and paths cleared so two groups never target the same app), named free among the groups ("Chrome (2)"),
    /// one undo step, selected so its form shows for the user to set the app and rename it. Its commands are copies: not on the
    /// Exclusions lists the originals' Also in or Not in name.
    /// </summary>
    private void DuplicateGroup(SectionItem section)
    {
        Guard(() =>
        {
            var group = _store.RequireGroup(section.Id.GroupId);
            var name = NameScope.Free(group.Name, _store.Current.Groups.Select(existing => existing.Name));
            var stored = _store.AddGroup(group.Duplicated(GroupId.New(), name));
            var id = SectionId.ForGroup(stored.Id);
            Expand(id);
            Select(id, null);
            ProjectSelection();
            var what = stored.Commands.Count == 1 ? "its command" : $"its {stored.Commands.Count} commands";
            Message = $"Duplicated '{group.Name}' as '{stored.Name}' with {what}. Set the app it is for in its form.";
        });
    }

    private async Task DeleteGroupAsync(SectionItem section)
    {
        // Its commands and those of its hold remaps, which go with it.
        var count = Sections.Where(listed => listed.Id.GroupId == section.Id.GroupId).Sum(listed => listed.Commands.Count);
        var question = count == 1 ? $"Delete group '{section.Name}' and its command?" : $"Delete group '{section.Name}' and its {count} commands?";
        await DeleteAsync("Delete app group", question, () => Deleted(_store.RemoveGroup(section.Id.GroupId).Name, EditKeymap.Current.Undo)).ConfigureAwait(true);
    }
}
