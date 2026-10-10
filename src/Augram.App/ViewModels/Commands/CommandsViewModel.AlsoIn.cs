using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The "Also in" half of <see cref="CommandsViewModel"/> (Joel, 2026-10-10, plan 0005 decision 7): the Exclusions › Global
/// entries a command whose trigger holds no stroke button (a button trigger, Right + wheel) still works over. The header's
/// Change… opens <see cref="AlsoInEditViewModel"/> in the shared form dialog, the entries without the disable-while-focused mode
/// by name as a check list; Save stores what is ticked as one <c>UpdateCommand</c>, one undo step, and nothing when nothing
/// changed. Cancel stores nothing. The rules (only such entries that exist, none under a hold remap or on a trigger that holds the
/// stroke button, sorted) are Core's normalisation, not this file's.
/// </summary>
public sealed partial class CommandsViewModel
{
    private async Task EditAlsoInAsync(CommandItem item)
    {
        if (!item.CanSetAlsoIn || _store.FindCommand(item.Id) is not { Command: { HoldRemapId: null } command })
        {
            return;
        }

        var edit = AlsoInEditViewModel.For(command, _store.Current);
        if (!await _dialogs.ShowAsync(new FormDialogRequest(AlsoInEditViewModel.Title, AlsoInEditViewModel.ConfirmLabel, Screen: edit.Declare())).ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var (group, stored) = RequireCommand(item.Id);
            var alsoIn = edit.AlsoIn;
            if (!stored.AlsoIn.ToHashSet().SetEquals(alsoIn))
            {
                _store.UpdateCommand(group.Id, stored with { AlsoIn = alsoIn });
            }
        });
    }
}
