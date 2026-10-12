using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The selected category's form in the Global tab's side panel (Joel, 2026-10-08: "Use on" per category, so a set of
/// Global commands can be kept off the Mac or the PC): its name and its Use on boxes (<see cref="CategoryEditViewModel"/>).
/// Each edit is one <see cref="MappingStore.UpdateGroup"/> of the category's group, one undo step; a refused name shows
/// its rule while the field keeps the text, and a refused Use on (unticking both) shows its rule and puts the boxes back,
/// since a check box cannot keep a value the store refused the way a text field can. Undo and a rename in the tree are
/// synced into the open form (<see cref="PanelEdit{TEdit, TId}"/>).
/// </summary>
public sealed partial class CommandsViewModel
{
    private PanelEdit<CategoryEditViewModel, (GroupId Group, CategoryId Category)>? _categoryPanel;

    private PanelEdit<CategoryEditViewModel, (GroupId Group, CategoryId Category)> CategoryPanel => _categoryPanel ??= new(OnCategoryEdited);

    private void ProjectCategoryPanel(GroupId groupId, CommandCategory category)
    {
        if (CategoryPanel.Show((groupId, category.Id), () => CategoryEditViewModel.From(category), edit => edit.SyncFrom(category)) is { } shown)
        {
            GroupForm = shown.Declare();
        }
    }

    private void OnCategoryEdited(CategoryEditViewModel edit, (GroupId Group, CategoryId Category) id, string? property)
    {
        Message = null;
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

        // A refused Use on puts the boxes back, leaving the name as typed.
        if (Message is not null && UseOnEdit.IsUseOn(property) && _store.FindGroup(id.Group)?.FindCategory(id.Category) is { } stored)
        {
            CategoryPanel.Sync(form => form.UseOn.SyncFrom(stored.UseOn));
        }
    }
}
