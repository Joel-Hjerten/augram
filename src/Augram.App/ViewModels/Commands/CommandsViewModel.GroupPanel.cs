using System.ComponentModel;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The selected app group's form in the side panel (Joel, 2026-10-07: selecting a group shows it, as selecting a
/// command shows its trigger and steps). Shown while an app group row, not a command, is selected. Each edit applies
/// at once, one undo step each like a step edit; a rule the store refuses (an empty or taken name) shows on the message
/// line and the field keeps what was typed. The form is rebuilt only when another group is selected, so typing never
/// loses focus; a change from elsewhere (undo, a rename or the active box in the tree) is synced into it, but the
/// panel's own edits are not echoed back.
/// </summary>
public sealed partial class CommandsViewModel
{
    private GroupEditViewModel? _groupEdit;
    private GroupId? _groupEditId;
    private bool _applyingGroupEdit;
    private bool _syncingGroupEdit;

    private void ProjectGroupPanel()
    {
        var section = SelectedCommandId is null && SelectedSectionId is { } id ? Sections.FirstOrDefault(s => s.Id == id) : null;
        var group = section is { CanEditDefinition: true } ? _store.FindGroup(section.Id.GroupId) : null;
        if (group is null)
        {
            DetachGroupEdit();
            GroupForm = null;
            return;
        }

        if (_groupEdit is null || _groupEditId != group.Id)
        {
            DetachGroupEdit();
            _groupEdit = GroupEditViewModel.From(group);
            _groupEditId = group.Id;
            _groupEdit.PropertyChanged += OnGroupEdited;
            GroupForm = _groupEdit.Declare();
        }
        else if (!_applyingGroupEdit)
        {
            _syncingGroupEdit = true;
            try
            {
                _groupEdit.SyncFrom(group);
            }
            finally
            {
                _syncingGroupEdit = false;
            }
        }
    }

    private void OnGroupEdited(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncingGroupEdit || _groupEdit is not { } edit || _groupEditId is not { } id)
        {
            return;
        }

        Message = null;
        _applyingGroupEdit = true;
        try
        {
            Guard(() => _store.UpdateGroup(edit.Apply(RequireGroup(id))));
        }
        finally
        {
            _applyingGroupEdit = false;
        }
    }

    private void DetachGroupEdit()
    {
        if (_groupEdit is not null)
        {
            _groupEdit.PropertyChanged -= OnGroupEdited;
        }

        _groupEdit = null;
        _groupEditId = null;
    }
}
