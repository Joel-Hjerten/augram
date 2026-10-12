using Augram.App.Components.ListEditing;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Components.CommandTree;

/// <summary>The context menu and key bindings of a <see cref="CommandTree"/> (F5a), on <see cref="ActionMenu{TAction}"/>; every entry ends in one <see cref="CommandTreeAction"/>.</summary>
internal static class CommandTreeMenu
{
    public static ContextMenu Build(Action<CommandTreeAction> request) => ActionMenu<CommandTreeAction>.Build(
        request,
        (string.Empty, CommandTreeAction.NewSection),
        ("New command", CommandTreeAction.NewCommand),
        ("New hold remap", CommandTreeAction.NewHoldRemap),
        ("Rename", CommandTreeAction.Rename),
        ("Delete", CommandTreeAction.Delete),
        ("Copy", CommandTreeAction.Copy),
        ("Paste", CommandTreeAction.Paste),
        ("Export…", CommandTreeAction.Export));

    /// <summary>
    /// Labels the new-section entry as the host does and shows or disables the entries that depend on the
    /// selection, as the section allows: rename and delete for any section or command, copy per <see cref="SectionItem"/>,
    /// New hold remap only where the section offers it (an app group, plan 0002), Export… only on a section header that exports
    /// (an app group or a Global category, plan 0003), and New command only with a section to put it in (Joel, 2026-10-11:
    /// nothing selected makes nothing); a command can always be renamed, deleted and copied.
    /// </summary>
    public static void Refresh(ContextMenu menu, SectionItem? section, CommandItem? command, string newSectionLabel)
    {
        foreach (var (item, action) in ActionMenu<CommandTreeAction>.Entries(menu))
        {
            switch (action)
            {
                case CommandTreeAction.NewSection:
                    item.Header = newSectionLabel;
                    break;
                case CommandTreeAction.NewHoldRemap or CommandTreeAction.Export:
                    item.IsVisible = Allows(action, section, command);
                    break;
                case CommandTreeAction.NewCommand:
                    item.IsEnabled = section is not null;
                    break;
                case CommandTreeAction.Rename or CommandTreeAction.Delete or CommandTreeAction.Copy:
                    item.IsEnabled = Allows(action, section, command);
                    break;
            }
        }
    }

    /// <summary>
    /// Whether the selection allows the action at all: a section only as its <see cref="SectionItem"/> says; Rename and Delete
    /// need a command or a section (every section has both); Copy a command or a section that copies itself (a hold remap);
    /// New hold remap an app group's section; Export a section header that exports, with no command selected.
    /// </summary>
    public static bool Allows(CommandTreeAction action, SectionItem? section, CommandItem? command) => action switch
    {
        CommandTreeAction.Rename or CommandTreeAction.Delete => command is not null || section is not null,
        CommandTreeAction.ToggleActive => command is not null || section is { CanToggleActive: true },
        CommandTreeAction.Copy => command is not null || section is { CanCopy: true },
        CommandTreeAction.NewHoldRemap => section is { CanAddHoldRemap: true },
        CommandTreeAction.Export => command is null && section is { CanExport: true },
        _ => true,
    };

    /// <summary><paramref name="canExecute"/> says no while a row's name editor has focus (<see cref="ActionMenu{TAction}.BindKeys"/>).</summary>
    public static void BindKeys(InputElement target, EditKeymap keymap, Action<CommandTreeAction> request, Func<bool> canExecute)
        => ActionMenu<CommandTreeAction>.BindKeys(
            target,
            request,
            canExecute,
            (keymap.New, CommandTreeAction.NewCommand),
            (keymap.Rename, CommandTreeAction.Rename),
            (keymap.RenameAlso, CommandTreeAction.Rename),
            (keymap.Delete, CommandTreeAction.Delete),
            (keymap.Copy, CommandTreeAction.Copy),
            (keymap.Paste, CommandTreeAction.Paste),
            (keymap.Undo, CommandTreeAction.Undo),
            (keymap.Redo, CommandTreeAction.Redo),
            (keymap.Find, CommandTreeAction.Find));
}
