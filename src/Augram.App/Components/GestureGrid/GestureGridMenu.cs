using Augram.App.Components.ListEditing;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Components.GestureGrid;

/// <summary>The context menu and key bindings of a <see cref="GestureGrid"/> (F5a), on <see cref="ActionMenu{TAction}"/>; every entry ends in one <see cref="GestureGridAction"/>. Import lives on the toolbar only; activation is per command, not per gesture (F3). "Used by…" is always offered: an unused gesture's popup says so.</summary>
internal static class GestureGridMenu
{
    public static ContextMenu Build(Action<GestureGridAction> request) => ActionMenu<GestureGridAction>.Build(
        request,
        ("New gesture…", GestureGridAction.New),
        ("Redraw…", GestureGridAction.Redraw),
        ("Rename", GestureGridAction.Rename),
        ("Clean up shape", GestureGridAction.CleanUp),
        ("Restore original", GestureGridAction.RestoreOriginal),
        ("Used by…", GestureGridAction.UsedBy),
        ("Delete", GestureGridAction.Delete),
        ("Keep this, delete its duplicates", GestureGridAction.KeepThis));

    /// <summary>Shows or hides the entries that only apply to some tiles: "Keep this" needs a duplicate; "Clean up shape" a gesture as drawn, "Restore original" a cleaned one.</summary>
    public static void Refresh(ContextMenu menu, GestureTileItem? selected)
    {
        foreach (var (item, action) in ActionMenu<GestureGridAction>.Entries(menu))
        {
            if (action is GestureGridAction.KeepThis)
            {
                item.IsVisible = selected?.HasDuplicates == true;
            }
            else if (action is GestureGridAction.CleanUp)
            {
                item.IsVisible = selected is { IsCleanedUp: false, SampleCount: > 0 };
            }
            else if (action is GestureGridAction.RestoreOriginal)
            {
                item.IsVisible = selected?.IsCleanedUp == true;
            }
        }
    }

    /// <summary>
    /// The Commands tab's keys (Q11, Joel 2026-10-11: Enter renames a gesture on Windows too, beside F2); <paramref name="canExecute"/>
    /// says no while a tile's name editor has focus (<see cref="ActionMenu{TAction}.BindKeys"/>).
    /// </summary>
    public static void BindKeys(InputElement target, EditKeymap keymap, Action<GestureGridAction> request, Func<bool> canExecute)
        => ActionMenu<GestureGridAction>.BindKeys(
            target,
            request,
            canExecute,
            (keymap.New, GestureGridAction.New),
            (keymap.Rename, GestureGridAction.Rename),
            (keymap.RenameAlso, GestureGridAction.Rename),
            (keymap.Delete, GestureGridAction.Delete),
            (keymap.Undo, GestureGridAction.Undo),
            (keymap.Redo, GestureGridAction.Redo),
            (keymap.Find, GestureGridAction.Find));
}
