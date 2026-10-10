using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The "Not in" half of <see cref="CommandsViewModel"/> (Joel, 2026-10-10, plan 0004): a Global command's app groups it is not
/// used in. The header's Change… opens <see cref="NotInEditViewModel"/> in the shared form dialog, the app groups by name as a
/// check list; Save stores what is ticked as one <c>UpdateCommand</c>, one undo step (none when nothing changed), Cancel
/// nothing. The rules (only Global keeps it, only groups that exist, sorted) are Core's normalisation, not this file's.
/// </summary>
public sealed partial class CommandsViewModel
{
    private async Task EditNotInAsync(CommandItem item)
    {
        if (_store.FindCommand(item.Id) is not { Group.IsGlobal: true, Command: { HoldRemapId: null } command })
        {
            return;
        }

        var edit = NotInEditViewModel.For(command, _store.Current);
        if (!await _dialogs.ShowAsync(new FormDialogRequest(NotInEditViewModel.Title, NotInEditViewModel.ConfirmLabel, Screen: edit.Declare())).ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var notIn = edit.NotIn;
            if (!RequireCommand(item.Id).Command.NotIn.ToHashSet().SetEquals(notIn))
            {
                UpdateCommand(item.Id, stored => stored with { NotIn = notIn });
            }
        });
    }
}
