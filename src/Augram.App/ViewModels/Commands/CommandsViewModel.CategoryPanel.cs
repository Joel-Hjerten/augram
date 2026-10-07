using System.ComponentModel;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The selected category's form in the Global tab's side panel (Joel, 2026-10-08: "Use on" per category, so a set of
/// Global commands can be kept off the Mac or the PC): its name and its Use on boxes (<see cref="CategoryEditViewModel"/>).
/// Each edit is one <see cref="MappingStore.UpdateGroup"/> of the category's group, one undo step; a refused name shows
/// its rule while the field keeps the text, and a refused Use on (unticking both) shows its rule and puts the boxes back,
/// since a check box cannot keep a value the store refused the way a text field can. Undo and a rename in the tree are
/// synced into the open form.
/// </summary>
public sealed partial class CommandsViewModel
{
    private CategoryEditViewModel? _categoryEdit;
    private (GroupId Group, CategoryId Category)? _categoryEditId;
    private bool _applyingCategoryEdit;
    private bool _syncingCategoryEdit;

    private void ProjectCategoryPanel(GroupId groupId, CommandCategory category)
    {
        if (_categoryEdit is null || _categoryEditId != (groupId, category.Id))
        {
            DetachCategoryEdit();
            _categoryEdit = CategoryEditViewModel.From(category);
            _categoryEditId = (groupId, category.Id);
            _categoryEdit.PropertyChanged += OnCategoryEdited;
            GroupForm = _categoryEdit.Declare();
        }
        else if (!_applyingCategoryEdit)
        {
            SyncCategoryEdit(edit => edit.SyncFrom(category));
        }
    }

    private void OnCategoryEdited(object? sender, PropertyChangedEventArgs e)
    {
        if (_syncingCategoryEdit || _categoryEdit is not { } edit || _categoryEditId is not { } id)
        {
            return;
        }

        Message = null;
        _applyingCategoryEdit = true;
        try
        {
            Guard(() =>
            {
                var group = RequireGroup(id.Group);
                if (group.FindCategory(id.Category) is null)
                {
                    throw new KeyNotFoundException("That category no longer exists.");
                }

                _store.UpdateGroup(group with
                {
                    Categories = [.. group.Categories.Select(category => category.Id == id.Category ? edit.Apply(category) : category)],
                });
            });
        }
        finally
        {
            _applyingCategoryEdit = false;
        }

        if (Message is not null && e.PropertyName is nameof(CategoryEditViewModel.UseOnWindows) or nameof(CategoryEditViewModel.UseOnMac)
            && _store.FindGroup(id.Group)?.FindCategory(id.Category) is { } stored)
        {
            SyncCategoryEdit(form => form.SyncUseOnFrom(stored));
        }
    }

    private void SyncCategoryEdit(Action<CategoryEditViewModel> sync)
    {
        if (_categoryEdit is not { } edit)
        {
            return;
        }

        _syncingCategoryEdit = true;
        try
        {
            sync(edit);
        }
        finally
        {
            _syncingCategoryEdit = false;
        }
    }

    private void DetachCategoryEdit()
    {
        if (_categoryEdit is not null)
        {
            _categoryEdit.PropertyChanged -= OnCategoryEdited;
        }

        _categoryEdit = null;
        _categoryEditId = null;
    }
}
