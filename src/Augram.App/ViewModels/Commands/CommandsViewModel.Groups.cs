using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>The app group half of <see cref="CommandsViewModel"/> (F5, F5a): the app group form for new and edit, rename, and delete with confirmation. The Global group is never renamed or deleted.</summary>
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
            Select(stored.Id, null);
            ProjectSelection();
        });
    }

    private async Task EditGroupAsync(GroupId id)
    {
        var group = RequireGroup(id);
        var edit = GroupEditViewModel.From(group);
        if (!await _dialogs.ShowAsync(new FormDialogRequest($"Edit app group '{group.Name}'", "Save", Screen: edit.Declare())).ConfigureAwait(true))
        {
            return;
        }

        Guard(() => _store.UpdateGroup(edit.Apply(RequireGroup(id))));
    }

    private void RenameGroup(GroupItem group, string name)
    {
        if (group.IsGlobal)
        {
            Message = "The Global group cannot be renamed.";
            return;
        }

        _store.UpdateGroup(RequireGroup(group.Id) with { Name = name });
    }

    private async Task DeleteGroupAsync(GroupItem group)
    {
        if (group.IsGlobal)
        {
            Message = "The Global group cannot be deleted.";
            return;
        }

        var count = group.Commands.Count;
        var question = count == 1 ? $"Delete group '{group.Name}' and its command?" : $"Delete group '{group.Name}' and its {count} commands?";
        if (!await _confirm.ConfirmAsync("Delete app group", question, "Delete").ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var removed = _store.RemoveGroup(group.Id);
            Message = $"Deleted '{removed.Name}'. {CommandsKeymap.Current.Undo} undoes it.";
        });
    }

    private AppGroup RequireGroup(GroupId id) => _store.FindGroup(id) ?? throw new KeyNotFoundException("That app group no longer exists.");
}
