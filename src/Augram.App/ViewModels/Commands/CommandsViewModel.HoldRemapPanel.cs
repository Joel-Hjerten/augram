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
/// and a rename in the tree are synced into the open form (<see cref="PanelEdit{TEdit, TId}"/>).
/// </summary>
public sealed partial class CommandsViewModel
{
    private PanelEdit<HoldRemapEditViewModel, (GroupId Group, HoldRemapId HoldRemap)>? _holdRemapPanel;

    private PanelEdit<HoldRemapEditViewModel, (GroupId Group, HoldRemapId HoldRemap)> HoldRemapPanel => _holdRemapPanel ??= new(OnHoldRemapEdited);

    private void ProjectHoldRemapPanel(GroupId groupId, HoldRemap holdRemap)
    {
        if (HoldRemapPanel.Show((groupId, holdRemap.Id), () => HoldRemapEditViewModel.From(holdRemap), edit => edit.SyncFrom(holdRemap)) is { } shown)
        {
            GroupForm = shown.Declare();
        }
    }

    private void OnHoldRemapEdited(HoldRemapEditViewModel edit, (GroupId Group, HoldRemapId HoldRemap) id, string? property)
    {
        Message = null;
        var applied = Guard(() =>
        {
            var stored = RequireHoldRemap(id.Group, id.HoldRemap);
            var next = edit.Apply(stored);
            if (next != stored)
            {
                _store.UpdateHoldRemap(id.Group, next);
            }
        });

        if (!applied && property != nameof(HoldRemapEditViewModel.Name) && _store.FindGroup(id.Group)?.FindHoldRemap(id.HoldRemap) is { } current)
        {
            HoldRemapPanel.Sync(form => form.SyncFrom(current));
        }
    }
}
