using Augram.App.Components.CommandTree;
using Augram.App.Dialogs;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The "Not in" half of <see cref="CommandsViewModel"/> (Joel, 2026-10-10, plan 0004): the Exclusions › Per command entries a
/// command (Global's or an app group's, not one under a hold remap) is not used over. The header's Change… opens
/// <see cref="NotInEditViewModel"/> in the shared form dialog, the entries by name as a check list with Add app… and its
/// magnifier, and an ✕ on an unused entry. Save stores what is ticked as one undo step: an <c>UpdateCommand</c>, or, when Add app…
/// made new entries or ✕ took some away, one <c>MappingStore.UpdateCommandChangingIgnored</c> that adds and removes them and updates
/// the command together; nothing when nothing changed. Cancel stores nothing, the new entries and the removals included. The rules (only entries that exist, none under a hold remap,
/// sorted) are Core's normalisation, not this file's.
/// </summary>
public sealed partial class CommandsViewModel
{
    private async Task EditNotInAsync(CommandItem item)
    {
        if (_store.FindCommand(item.Id) is not { Command: { HoldRemapId: null } command })
        {
            return;
        }

        var edit = NotInEditViewModel.For(command, _store.Current, _platform);
        if (!await _dialogs.ShowAsync(edit).ConfigureAwait(true))
        {
            return;
        }

        Guard(() =>
        {
            var (group, stored) = _store.RequireCommand(item.Id);
            var notIn = edit.NotIn;
            if (edit.Added.Count > 0 || edit.Removed.Count > 0)
            {
                _store.UpdateCommandChangingIgnored(group.Id, stored with { NotIn = notIn }, edit.Added, edit.Removed);
            }
            else if (!stored.NotIn.ToHashSet().SetEquals(notIn))
            {
                _store.UpdateCommand(group.Id, stored with { NotIn = notIn });
            }
        });
    }
}
