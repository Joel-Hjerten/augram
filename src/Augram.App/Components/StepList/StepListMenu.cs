using Augram.App.Components.ListEditing;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Components.StepList;

/// <summary>
/// The context menu and key bindings of a <see cref="StepList"/> (F5a), on <see cref="ActionMenu{TAction}"/>; every entry ends in
/// one <see cref="StepListAction"/>. New step… and Change to… (Joel, 2026-10-11; no key) open the list's step type picker first,
/// which raises Add or ChangeType.
/// </summary>
internal static class StepListMenu
{
    public static ContextMenu Build(Action<StepListAction> request) => ActionMenu<StepListAction>.Build(
        request,
        ("New step…", StepListAction.Add),
        ("Change to…", StepListAction.ChangeType),
        ("Duplicate below", StepListAction.Duplicate),
        ("Copy", StepListAction.Copy),
        ("Paste at bottom", StepListAction.Paste),
        ("Delete", StepListAction.Delete));

    /// <summary>Entries that need a selected step (Change to…, Duplicate below, Copy, Delete) are disabled without one, and New step… while no type can be added (<see cref="StepList.CanAddStep"/>).</summary>
    public static void Refresh(ContextMenu menu, StepItem? selected, bool canAdd)
    {
        foreach (var (item, action) in ActionMenu<StepListAction>.Entries(menu))
        {
            if (action is StepListAction.ChangeType or StepListAction.Duplicate or StepListAction.Copy or StepListAction.Delete)
            {
                item.IsEnabled = selected is not null;
            }
            else if (action is StepListAction.Add)
            {
                item.IsEnabled = canAdd;
            }
        }
    }

    /// <summary><paramref name="canExecute"/> says no while the expanded step's form has keyboard focus, so Delete and Ctrl+D edit the field, not the list.</summary>
    public static void BindKeys(InputElement target, EditKeymap keymap, Action<StepListAction> request, Func<bool> canExecute)
        => ActionMenu<StepListAction>.BindKeys(
            target,
            request,
            canExecute,
            (keymap.New, StepListAction.Add),
            (keymap.Duplicate, StepListAction.Duplicate),
            (keymap.Copy, StepListAction.Copy),
            (keymap.Paste, StepListAction.Paste),
            (keymap.Delete, StepListAction.Delete),
            (keymap.Undo, StepListAction.Undo),
            (keymap.Redo, StepListAction.Redo));
}
