using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.Components.CommandTree;

/// <summary>Builds the context menu and key bindings of a <see cref="CommandTree"/> (F5a); every entry ends in one <see cref="CommandTreeAction"/>.</summary>
internal static class CommandTreeMenu
{
    public static ContextMenu Build(Action<CommandTreeAction> request)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("New group…", CommandTreeAction.NewGroup, request));
        menu.Items.Add(Item("New command", CommandTreeAction.NewCommand, request));
        menu.Items.Add(Item("Edit app definition…", CommandTreeAction.EditGroup, request));
        menu.Items.Add(Item("Rename", CommandTreeAction.Rename, request));
        menu.Items.Add(Item("Delete", CommandTreeAction.Delete, request));
        menu.Items.Add(Item("Copy", CommandTreeAction.Copy, request));
        menu.Items.Add(Item("Paste", CommandTreeAction.Paste, request));
        return menu;
    }

    /// <summary>Shows or disables the entries that depend on the selection: the Global group is never renamed, deleted or redefined; only a command can be copied.</summary>
    public static void Refresh(ContextMenu menu, GroupItem? group, CommandItem? command)
    {
        ArgumentNullException.ThrowIfNull(menu);
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            switch (item.Tag)
            {
                case CommandTreeAction.EditGroup:
                    item.IsVisible = command is null && group is { IsGlobal: false };
                    break;
                case CommandTreeAction.Rename:
                    item.IsEnabled = command is not null || group is { IsGlobal: false };
                    break;
                case CommandTreeAction.Delete:
                    item.IsEnabled = command is not null || group is { IsGlobal: false };
                    break;
                case CommandTreeAction.Copy:
                    item.IsEnabled = command is not null;
                    break;
            }
        }
    }

    /// <summary>
    /// Key bindings are evaluated from the focused element up before the key reaches it, so
    /// <paramref name="canExecute"/> must say no while a row's name editor has focus, or Delete and
    /// Ctrl+Z would act on the row instead of the text.
    /// </summary>
    public static void BindKeys(InputElement target, CommandsKeymap keymap, Action<CommandTreeAction> request, Func<bool> canExecute)
    {
        Bind(target, keymap.New, CommandTreeAction.NewCommand, request, canExecute);
        Bind(target, keymap.Rename, CommandTreeAction.Rename, request, canExecute);
        Bind(target, keymap.Delete, CommandTreeAction.Delete, request, canExecute);
        Bind(target, keymap.Copy, CommandTreeAction.Copy, request, canExecute);
        Bind(target, keymap.Paste, CommandTreeAction.Paste, request, canExecute);
        Bind(target, keymap.Undo, CommandTreeAction.Undo, request, canExecute);
        Bind(target, keymap.Redo, CommandTreeAction.Redo, request, canExecute);
    }

    private static MenuItem Item(string header, CommandTreeAction action, Action<CommandTreeAction> request)
    {
        var item = new MenuItem { Header = header, Tag = action };
        item.Click += (_, _) => request(action);
        return item;
    }

    private static void Bind(InputElement target, string gesture, CommandTreeAction action, Action<CommandTreeAction> request, Func<bool> canExecute)
        => target.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse(gesture), Command = new RelayCommand(() => request(action), canExecute) });
}
