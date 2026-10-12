using Augram.App.Components.ListEditing;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Components.MasterDetail;

/// <summary>
/// The context menu and key bindings of a <see cref="MasterDetail"/> list (on <see cref="ActionMenu{TAction}"/>), on the one
/// per-platform keymap (<see cref="EditKeymap"/>: the rename keys, Delete, new, undo, redo); every entry ends in one
/// <see cref="MasterDetailAction"/>. The move entry shows only while the host gives it a label (<see cref="MasterDetail.MoveLabel"/>).
/// </summary>
internal static class MasterDetailMenu
{
    public static ContextMenu Build(string newLabel, Action<MasterDetailAction> request) => ActionMenu<MasterDetailAction>.Build(
        request,
        (newLabel, MasterDetailAction.New),
        ("Rename", MasterDetailAction.Rename),
        (string.Empty, MasterDetailAction.Move),
        ("Delete", MasterDetailAction.Delete));

    /// <summary>Labels the new and move entries as the host does (no move label hides the move entry); rename, move and delete need a selected row.</summary>
    public static void Refresh(ContextMenu menu, string newLabel, string? moveLabel, bool hasSelection)
    {
        foreach (var (item, action) in ActionMenu<MasterDetailAction>.Entries(menu))
        {
            if (action is MasterDetailAction.New)
            {
                item.Header = newLabel;
                continue;
            }

            if (action is MasterDetailAction.Move)
            {
                item.Header = moveLabel;
                item.IsVisible = !string.IsNullOrEmpty(moveLabel);
            }

            item.IsEnabled = hasSelection;
        }
    }

    /// <summary><paramref name="canExecute"/> says no while a row's name editor has focus (<see cref="ActionMenu{TAction}.BindKeys"/>).</summary>
    public static void BindKeys(InputElement target, EditKeymap keymap, Action<MasterDetailAction> request, Func<bool> canExecute)
        => ActionMenu<MasterDetailAction>.BindKeys(
            target,
            request,
            canExecute,
            (keymap.New, MasterDetailAction.New),
            (keymap.Rename, MasterDetailAction.Rename),
            (keymap.RenameAlso, MasterDetailAction.Rename),
            (keymap.Delete, MasterDetailAction.Delete),
            (keymap.Undo, MasterDetailAction.Undo),
            (keymap.Redo, MasterDetailAction.Redo));
}
