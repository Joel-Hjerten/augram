using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The side panel's form for a selected section header (<see cref="GroupForm"/>): an app group's on the Apps tab (Joel,
/// 2026-10-07: selecting a group shows it, as selecting a command shows its trigger and steps), a hold remap's there too
/// (<c>.HoldRemapPanel</c>, plan 0002) and a category's on the Global tab (<c>.CategoryPanel</c>, Joel 2026-10-08;
/// Uncategorized's too, an ordinary category since Joel's 2026-10-11). Shown while a section row, not a command, is selected. Each edit applies at once, one undo step
/// each like a step edit; a rule the store refuses (an empty or taken name) shows on the message line and the field keeps
/// what was typed. Each form's lifecycle is a <see cref="PanelEdit{TEdit, TId}"/>: rebuilt only when another section is
/// selected, so typing never loses focus; a change from elsewhere (undo, a rename or the active box in the tree) is synced
/// into it, but the panel's own edits are not echoed back.
/// </summary>
public sealed partial class CommandsViewModel
{
    private PanelEdit<GroupEditViewModel, GroupId>? _groupPanel;

    // GuessText is computed from the fields; its notice follows a real edit that is applied already.
    private PanelEdit<GroupEditViewModel, GroupId> GroupPanel => _groupPanel ??= new(OnGroupEdited, nameof(AppMatcherEditViewModel.GuessText));

    /// <summary>Shows the selected app group's, hold remap's or category's form, or none.</summary>
    private void ProjectSidePanel()
    {
        var section = SelectedCommandId is null && SelectedSectionId is { } id ? Sections.FirstOrDefault(s => s.Id == id) : null;
        var group = section is null ? null : _store.FindGroup(section.Id.GroupId);
        if (section is { CanEditDefinition: true } && group is not null)
        {
            CategoryPanel.Close();
            HoldRemapPanel.Close();
            ProjectGroupPanel(group);
        }
        else if (section?.Id.HoldRemapId is { } holdRemapId && group?.FindHoldRemap(holdRemapId) is { } holdRemap)
        {
            GroupPanel.Close();
            CategoryPanel.Close();
            ProjectHoldRemapPanel(group.Id, holdRemap);
        }
        else if (section?.Id.CategoryId is { } categoryId && group?.FindCategory(categoryId) is { } category)
        {
            GroupPanel.Close();
            HoldRemapPanel.Close();
            ProjectCategoryPanel(group.Id, category);
        }
        else
        {
            GroupPanel.Close();
            CategoryPanel.Close();
            HoldRemapPanel.Close();
            GroupForm = null;
        }
    }

    private void ProjectGroupPanel(AppGroup group)
    {
        if (GroupPanel.Show(group.Id, () => GroupEditViewModel.From(group), edit => edit.SyncFrom(group)) is { } shown)
        {
            GroupForm = shown.Declare();
        }
    }

    private void OnGroupEdited(GroupEditViewModel edit, GroupId id, string? property)
    {
        Message = null;
        Guard(() => _store.UpdateGroup(edit.Apply(RequireGroup(id))));
    }
}
