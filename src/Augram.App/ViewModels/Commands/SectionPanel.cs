using Augram.App.Components.CommandTree;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The side panel's form for a selected section header (<see cref="CommandsViewModel.GroupForm"/>) of
/// <see cref="CommandsViewModel"/>: an app group's on the Apps tab (Joel, 2026-10-07: selecting a group shows it, as selecting a
/// command shows its trigger and steps), a hold remap's there too (plan 0002) and a category's on the Global tab (Joel
/// 2026-10-08; Uncategorized's too, an ordinary category since Joel's 2026-10-11). Shown while a section row, not a command, is
/// selected. Each edit applies at once, one undo step each like a step edit; a rule the store refuses (an empty or taken name)
/// shows on the message line and the field keeps what was typed. Each form's lifecycle is a
/// <see cref="PanelEdit{TEdit, TId}"/>: rebuilt only when another section is selected, so typing never loses focus; a change
/// from elsewhere (undo, a rename or the active box in the tree) is synced into it, but the panel's own edits are not echoed
/// back. The view model calls <see cref="Show"/> at the end of every projection of the selection.
/// </summary>
internal sealed class SectionPanel
{
    private readonly MappingStore _store;
    private readonly ICommandsHost _host;

    // GuessText is computed from the fields; its notice follows a real edit that is applied already.
    private readonly PanelEdit<GroupEditViewModel, GroupId> _group;
    private readonly PanelEdit<HoldRemapEditViewModel, (GroupId Group, HoldRemapId HoldRemap)> _holdRemap;
    private readonly PanelEdit<CategoryEditViewModel, (GroupId Group, CategoryId Category)> _category;

    public SectionPanel(MappingStore store, ICommandsHost host)
    {
        _store = store;
        _host = host;
        _group = new(OnGroupEdited, nameof(AppMatcherEditViewModel.GuessText));
        _holdRemap = new(OnHoldRemapEdited);
        _category = new(OnCategoryEdited);
    }

    /// <summary>Shows the form of <paramref name="section"/> (the selected section row; null while a command or nothing is selected): an app group's, a hold remap's, a category's, or none.</summary>
    public void Show(SectionItem? section)
    {
        var group = section is null ? null : _store.FindGroup(section.Id.GroupId);
        if (section is { CanEditDefinition: true } && group is not null)
        {
            _category.Close();
            _holdRemap.Close();
            ShowGroup(group);
        }
        else if (section?.Id.HoldRemapId is { } holdRemapId && group?.FindHoldRemap(holdRemapId) is { } holdRemap)
        {
            _group.Close();
            _category.Close();
            ShowHoldRemap(group.Id, holdRemap);
        }
        else if (section?.Id.CategoryId is { } categoryId && group?.FindCategory(categoryId) is { } category)
        {
            _group.Close();
            _holdRemap.Close();
            ShowCategory(group.Id, category);
        }
        else
        {
            _group.Close();
            _category.Close();
            _holdRemap.Close();
            _host.ShowForm(null);
        }
    }

    private void ShowGroup(AppGroup group)
    {
        if (_group.Show(group.Id, () => GroupEditViewModel.From(group), edit => edit.SyncFrom(group)) is { } shown)
        {
            _host.ShowForm(shown.Declare());
        }
    }

    private void OnGroupEdited(GroupEditViewModel edit, GroupId id, string? property)
    {
        _host.Message = null;
        _host.Guard(() => _store.UpdateGroup(edit.Apply(_store.RequireGroup(id))));
    }

    /// <summary>
    /// The selected hold remap's form (F9, plan 0002 step 4; as a category's on the Global tab): name, hold key, tap time, active
    /// and Use on (<see cref="HoldRemapEditViewModel"/>).
    /// </summary>
    private void ShowHoldRemap(GroupId groupId, HoldRemap holdRemap)
    {
        if (_holdRemap.Show((groupId, holdRemap.Id), () => HoldRemapEditViewModel.From(holdRemap), edit => edit.SyncFrom(holdRemap)) is { } shown)
        {
            _host.ShowForm(shown.Declare());
        }
    }

    /// <summary>
    /// Each edit is one <see cref="MappingStore.UpdateHoldRemap"/>, one undo step (an edit that changes nothing stored makes
    /// none: a hold key and the name that follows it are one step); a refused name shows its rule while the field keeps the
    /// text, and any other refused edit (a hold key the group already uses, both Use on boxes unticked) shows its rule and puts
    /// the form back on the stored values, since a capture field or a check box cannot keep a value the store refused the way a
    /// text field can.
    /// </summary>
    private void OnHoldRemapEdited(HoldRemapEditViewModel edit, (GroupId Group, HoldRemapId HoldRemap) id, string? property)
    {
        _host.Message = null;
        var applied = _host.Guard(() =>
        {
            var stored = _store.RequireHoldRemap(id.Group, id.HoldRemap);
            var next = edit.Apply(stored);
            if (next != stored)
            {
                _store.UpdateHoldRemap(id.Group, next);
            }
        });

        if (!applied && property != nameof(HoldRemapEditViewModel.Name) && _store.FindGroup(id.Group)?.FindHoldRemap(id.HoldRemap) is { } current)
        {
            _holdRemap.Sync(form => form.SyncFrom(current));
        }
    }

    /// <summary>
    /// The selected category's form (Joel, 2026-10-08: "Use on" per category, so a set of Global commands can be kept off the
    /// Mac or the PC): its name and its Use on boxes (<see cref="CategoryEditViewModel"/>).
    /// </summary>
    private void ShowCategory(GroupId groupId, CommandCategory category)
    {
        if (_category.Show((groupId, category.Id), () => CategoryEditViewModel.From(category), edit => edit.SyncFrom(category)) is { } shown)
        {
            _host.ShowForm(shown.Declare());
        }
    }

    /// <summary>
    /// Each edit is one <see cref="MappingStore.UpdateGroup"/> of the category's group, one undo step; a refused name shows its
    /// rule while the field keeps the text, and a refused Use on (unticking both) shows its rule and puts the boxes back, since a
    /// check box cannot keep a value the store refused the way a text field can.
    /// </summary>
    private void OnCategoryEdited(CategoryEditViewModel edit, (GroupId Group, CategoryId Category) id, string? property)
    {
        _host.Message = null;
        _host.Guard(() =>
        {
            var group = _store.RequireGroup(id.Group);
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
        if (_host.Message is not null && UseOnEdit.IsUseOn(property) && _store.FindGroup(id.Group)?.FindCategory(id.Category) is { } stored)
        {
            _category.Sync(form => form.UseOn.SyncFrom(stored.UseOn));
        }
    }
}
