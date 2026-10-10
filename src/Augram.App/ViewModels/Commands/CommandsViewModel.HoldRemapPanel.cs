using System.ComponentModel;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The selected hold remap's form in the Apps tab's side panel (F9, plan 0002 step 4; as a category's on the Global tab):
/// name, hold key, tap time, active and Use on (<see cref="HoldRemapEditViewModel"/>). Each edit is one
/// <see cref="MappingStore.UpdateHoldRemap"/>, one undo step (an edit that changes nothing stored makes none: a hold key and
/// the name that follows it are one step); a refused name shows its rule while the field keeps the text, and any other
/// refused edit (a hold key the group already uses, both Use on boxes unticked) shows its rule and puts the form back on the
/// stored values, since a capture field or a check box cannot keep a value the store refused the way a text field can. Undo
/// and a rename in the tree are synced into the open form.
/// </summary>
public sealed partial class CommandsViewModel
{
    private HoldRemapEditViewModel? _holdRemapEdit;
    private (GroupId Group, HoldRemapId HoldRemap)? _holdRemapEditId;
    private bool _applyingHoldRemapEdit;
    private bool _syncingHoldRemapEdit;

    private void ProjectHoldRemapPanel(GroupId groupId, HoldRemap holdRemap)
    {
        if (_holdRemapEdit is null || _holdRemapEditId != (groupId, holdRemap.Id))
        {
            DetachHoldRemapEdit();
            _holdRemapEdit = HoldRemapEditViewModel.From(holdRemap);
            _holdRemapEditId = (groupId, holdRemap.Id);
            _holdRemapEdit.PropertyChanged += OnHoldRemapEdited;
            GroupForm = _holdRemapEdit.Declare();
        }
        else if (!_applyingHoldRemapEdit)
        {
            SyncHoldRemapEdit(holdRemap);
        }
    }

    private void OnHoldRemapEdited(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncingHoldRemapEdit || _holdRemapEdit is not { } edit || _holdRemapEditId is not { } id)
        {
            return;
        }

        Message = null;
        _applyingHoldRemapEdit = true;
        bool applied;
        try
        {
            applied = Guard(() =>
            {
                var stored = RequireHoldRemap(id.Group, id.HoldRemap);
                var next = edit.Apply(stored);
                if (next != stored)
                {
                    _store.UpdateHoldRemap(id.Group, next);
                }
            });
        }
        finally
        {
            _applyingHoldRemapEdit = false;
        }

        if (!applied && e.PropertyName != nameof(HoldRemapEditViewModel.Name) && _store.FindGroup(id.Group)?.FindHoldRemap(id.HoldRemap) is { } current)
        {
            SyncHoldRemapEdit(current);
        }
    }

    private void SyncHoldRemapEdit(HoldRemap holdRemap)
    {
        if (_holdRemapEdit is not { } edit)
        {
            return;
        }

        _syncingHoldRemapEdit = true;
        try
        {
            edit.SyncFrom(holdRemap);
        }
        finally
        {
            _syncingHoldRemapEdit = false;
        }
    }

    private void DetachHoldRemapEdit()
    {
        if (_holdRemapEdit is not null)
        {
            _holdRemapEdit.PropertyChanged -= OnHoldRemapEdited;
        }

        _holdRemapEdit = null;
        _holdRemapEditId = null;
    }
}
