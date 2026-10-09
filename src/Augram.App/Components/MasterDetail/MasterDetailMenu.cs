using Augram.App.Components.CommandTree;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.Components.MasterDetail;

/// <summary>
/// The context menu and key bindings of a <see cref="MasterDetail"/> list, on the Commands tab's one per-platform keymap
/// (<see cref="CommandsKeymap"/>: the rename key, Delete, new, undo, redo); every entry ends in one <see cref="MasterDetailAction"/>.
/// </summary>
internal static class MasterDetailMenu
{
    public static ContextMenu Build(string newLabel, Action<MasterDetailAction> request)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item(newLabel, MasterDetailAction.New, request));
        menu.Items.Add(Item("Rename", MasterDetailAction.Rename, request));
        menu.Items.Add(Item("Delete", MasterDetailAction.Delete, request));
        return menu;
    }

    /// <summary>Labels the new entry as the host does; rename and delete need a selected row.</summary>
    public static void Refresh(ContextMenu menu, string newLabel, bool hasSelection)
    {
        ArgumentNullException.ThrowIfNull(menu);
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is MasterDetailAction.New)
            {
                item.Header = newLabel;
            }
            else
            {
                item.IsEnabled = hasSelection;
            }
        }
    }

    /// <summary>
    /// Key bindings are evaluated from the focused element up before the key reaches it, so <paramref name="canExecute"/>
    /// must say no while a row's name editor has focus, or Delete and the undo key would act on the row instead of the text.
    /// </summary>
    public static void BindKeys(InputElement target, CommandsKeymap keymap, Action<MasterDetailAction> request, Func<bool> canExecute)
    {
        Bind(target, keymap.New, MasterDetailAction.New, request, canExecute);
        Bind(target, keymap.Rename, MasterDetailAction.Rename, request, canExecute);
        if (keymap.RenameAlso is { } renameAlso)
        {
            Bind(target, renameAlso, MasterDetailAction.Rename, request, canExecute);
        }

        Bind(target, keymap.Delete, MasterDetailAction.Delete, request, canExecute);
        Bind(target, keymap.Undo, MasterDetailAction.Undo, request, canExecute);
        Bind(target, keymap.Redo, MasterDetailAction.Redo, request, canExecute);
    }

    private static MenuItem Item(string header, MasterDetailAction action, Action<MasterDetailAction> request)
    {
        var item = new MenuItem { Header = header, Tag = action };
        item.Click += (_, _) => request(action);
        return item;
    }

    private static void Bind(InputElement target, string gesture, MasterDetailAction action, Action<MasterDetailAction> request, Func<bool> canExecute)
        => target.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse(gesture), Command = new RelayCommand(() => request(action), canExecute) });
}
