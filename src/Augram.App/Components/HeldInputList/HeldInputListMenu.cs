using Augram.App.Components.ListEditing;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Components.HeldInputList;

/// <summary>
/// The context menu and key bindings of a <see cref="HeldInputList"/> (plan 0012 decision 2: copy, paste, delete and undo as on
/// steps), on <see cref="ActionMenu{TAction}"/>; New… opens the list's recorder first, the rest end in one
/// <see cref="HeldInputListAction"/>.
/// </summary>
internal static class HeldInputListMenu
{
    /// <summary>The menu's New…: it opens the recorder, which raises Add once an input is chosen.</summary>
    public const HeldInputListAction New = HeldInputListAction.Add;

    public static ContextMenu Build(Action<HeldInputListAction> request) => ActionMenu<HeldInputListAction>.Build(
        request,
        ("New…", New),
        ("Copy", HeldInputListAction.Copy),
        ("Paste at bottom", HeldInputListAction.Paste),
        ("Delete", HeldInputListAction.Delete));

    /// <summary>Copy and Delete need a selected row.</summary>
    public static void Refresh(ContextMenu menu, HeldInputItem? selected)
    {
        foreach (var (item, action) in ActionMenu<HeldInputListAction>.Entries(menu))
        {
            if (action is HeldInputListAction.Copy or HeldInputListAction.Delete)
            {
                item.IsEnabled = selected is not null;
            }
        }
    }

    /// <summary><paramref name="canExecute"/> says no while the recorder has the keyboard, so a key being recorded never edits the list.</summary>
    public static void BindKeys(InputElement target, EditKeymap keymap, Action<HeldInputListAction> request, Func<bool> canExecute)
        => ActionMenu<HeldInputListAction>.BindKeys(
            target,
            request,
            canExecute,
            (keymap.New, New),
            (keymap.Copy, HeldInputListAction.Copy),
            (keymap.Paste, HeldInputListAction.Paste),
            (keymap.Delete, HeldInputListAction.Delete),
            (keymap.Undo, HeldInputListAction.Undo),
            (keymap.Redo, HeldInputListAction.Redo));
}
