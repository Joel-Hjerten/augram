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
        menu.Items.Add(Item(string.Empty, CommandTreeAction.NewSection, request));
        menu.Items.Add(Item("New command", CommandTreeAction.NewCommand, request));
        menu.Items.Add(Item("Edit app definition…", CommandTreeAction.EditGroup, request));
        menu.Items.Add(Item("Rename", CommandTreeAction.Rename, request));
        menu.Items.Add(Item("Delete", CommandTreeAction.Delete, request));
        menu.Items.Add(Item("Copy", CommandTreeAction.Copy, request));
        menu.Items.Add(Item("Paste", CommandTreeAction.Paste, request));
        return menu;
    }

    /// <summary>
    /// Labels the new-section entry as the host does and shows or disables the entries that depend on the
    /// selection, as the section allows: app definition, rename and delete per <see cref="SectionItem"/>;
    /// a command can always be renamed, deleted and copied.
    /// </summary>
    public static void Refresh(ContextMenu menu, SectionItem? section, CommandItem? command, string newSectionLabel)
    {
        ArgumentNullException.ThrowIfNull(menu);
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            switch (item.Tag)
            {
                case CommandTreeAction.NewSection:
                    item.Header = newSectionLabel;
                    break;
                case CommandTreeAction.EditGroup:
                    item.IsVisible = command is null && Allows(CommandTreeAction.EditGroup, section, command);
                    break;
                case CommandTreeAction.Rename:
                    item.IsEnabled = command is not null || section is { CanRename: true };
                    break;
                case CommandTreeAction.Delete or CommandTreeAction.Copy:
                    item.IsEnabled = Allows((CommandTreeAction)item.Tag, section, command);
                    break;
            }
        }
    }

    /// <summary>Whether the selection allows the action at all: a section only as its <see cref="SectionItem"/> says; Copy needs a command.</summary>
    public static bool Allows(CommandTreeAction action, SectionItem? section, CommandItem? command) => action switch
    {
        CommandTreeAction.Delete => command is not null || section is { CanDelete: true },
        CommandTreeAction.ToggleActive => command is not null || section is { CanToggleActive: true },
        CommandTreeAction.Copy => command is not null,
        CommandTreeAction.EditGroup => section is { CanEditDefinition: true },
        _ => true,
    };

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
