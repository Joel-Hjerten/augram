using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>The app group half of <see cref="CommandsViewModel"/> (F5, F5a; the Apps tab's sections): the app group form for a new group, rename, and delete with confirmation (its hold remaps and their commands go with it); the selected group's form lives in <c>.GroupPanel</c>. The Global group is never renamed or deleted.</summary>
public sealed partial class CommandsViewModel
{
    private async Task NewGroupAsync()
    {
        var edit = new GroupEditViewModel();
        if (!await _dialogs.ShowAsync(new FormDialogRequest("New app group", "Create", Screen: edit.Declare())).ConfigureAwait(true))
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

    private void RenameGroup(GroupId id, string name)
    {
        if (id == GroupId.Global)
        {
            Message = "The Global group cannot be renamed.";
            return;
        }

        _store.UpdateGroup(RequireGroup(id) with { Name = name });
    }

    private async Task DeleteGroupAsync(SectionItem section)
    {
        if (section.Id.GroupId == GroupId.Global)
        {
            Message = "The Global group cannot be deleted.";
            return;
        }

        // Its commands and those of its hold remaps, which go with it.
        var count = Sections.Where(listed => listed.Id.GroupId == section.Id.GroupId).Sum(listed => listed.Commands.Count);
        var question = count == 1 ? $"Delete group '{section.Name}' and its command?" : $"Delete group '{section.Name}' and its {count} commands?";
        if (!await _confirm.ConfirmAsync("Delete app group", question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var removed = _store.RemoveGroup(section.Id.GroupId);
            Message = $"Deleted '{removed.Name}'. {CommandsKeymap.Current.Undo} undoes it.";
        });
    }

    private AppGroup RequireGroup(GroupId id) => _store.FindGroup(id) ?? throw new KeyNotFoundException("That app group no longer exists.");
}
