using System.ComponentModel;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The side panel's form for a selected section header (<see cref="GroupForm"/>): an app group's on the Apps tab (Joel,
/// 2026-10-07: selecting a group shows it, as selecting a command shows its trigger and steps), a hold remap's there too
/// (<c>.HoldRemapPanel</c>, plan 0002) and a category's on the Global tab (<c>.CategoryPanel</c>, Joel 2026-10-08);
/// Uncategorized has none. Shown while a section row, not a command, is selected. Each edit applies at once, one undo step
/// each like a step edit; a rule the store refuses (an empty or taken name) shows on the message line and the field keeps
/// what was typed. The form is rebuilt only when another section is selected, so typing never loses focus; a change from
/// elsewhere (undo, a rename or the active box in the tree) is synced into it, but the panel's own edits are not echoed back.
/// </summary>
public sealed partial class CommandsViewModel
{
    private GroupEditViewModel? _groupEdit;
    private GroupId? _groupEditId;
    private bool _applyingGroupEdit;
    private bool _syncingGroupEdit;

    /// <summary>Shows the selected app group's, hold remap's or category's form, or none.</summary>
    private void ProjectSidePanel()
    {
        var section = SelectedCommandId is null && SelectedSectionId is { } id ? Sections.FirstOrDefault(s => s.Id == id) : null;
        var group = section is null ? null : _store.FindGroup(section.Id.GroupId);
        if (section is { CanEditDefinition: true } && group is not null)
        {
            DetachCategoryEdit();
            DetachHoldRemapEdit();
            ProjectGroupPanel(group);
        }
        else if (section?.Id.HoldRemapId is { } holdRemapId && group?.FindHoldRemap(holdRemapId) is { } holdRemap)
        {
            DetachGroupEdit();
            DetachCategoryEdit();
            ProjectHoldRemapPanel(group.Id, holdRemap);
        }
        else if (section?.Id.CategoryId is { } categoryId && group?.FindCategory(categoryId) is { } category)
        {
            DetachGroupEdit();
            DetachHoldRemapEdit();
            ProjectCategoryPanel(group.Id, category);
        }
        else
        {
            DetachGroupEdit();
            DetachCategoryEdit();
            DetachHoldRemapEdit();
            GroupForm = null;
        }
    }

    private void ProjectGroupPanel(AppGroup group)
    {
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
        // GuessText is computed from the fields; its notice follows a real edit that is applied already.
        if (_syncingGroupEdit || e.PropertyName == nameof(AppMatcherEditViewModel.GuessText) || _groupEdit is not { } edit || _groupEditId is not { } id)
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
