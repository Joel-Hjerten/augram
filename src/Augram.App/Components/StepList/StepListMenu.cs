using Augram.App.Components.CommandTree;
using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.Components.StepList;

/// <summary>Builds the context menu and key bindings of a <see cref="StepList"/> (F5a); every entry ends in one <see cref="StepListAction"/>.</summary>
internal static class StepListMenu
{
    public static ContextMenu Build(Action<StepListAction> request)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("New step…", StepListAction.Add, request));
        menu.Items.Add(Item("Duplicate below", StepListAction.Duplicate, request));
        menu.Items.Add(Item("Copy", StepListAction.Copy, request));
        menu.Items.Add(Item("Paste at bottom", StepListAction.Paste, request));
        menu.Items.Add(Item("Delete", StepListAction.Delete, request));
        return menu;
    }

    /// <summary>Entries that need a selected step are disabled without one, and New step… while no type can be added (<see cref="StepList.CanAddStep"/>).</summary>
    public static void Refresh(ContextMenu menu, StepItem? selected, bool canAdd)
    {
        ArgumentNullException.ThrowIfNull(menu);
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            if (item.Tag is StepListAction.Duplicate or StepListAction.Copy or StepListAction.Delete)
            {
                item.IsEnabled = selected is not null;
            }
            else if (item.Tag is StepListAction.Add)
            {
                item.IsEnabled = canAdd;
            }
        }
    }

    /// <summary><paramref name="canExecute"/> says no while the expanded step's form has keyboard focus, so Delete and Ctrl+D edit the field, not the list.</summary>
    public static void BindKeys(InputElement target, CommandsKeymap keymap, Action<StepListAction> request, Func<bool> canExecute)
    {
        Bind(target, keymap.New, StepListAction.Add, request, canExecute);
        Bind(target, keymap.Duplicate, StepListAction.Duplicate, request, canExecute);
        Bind(target, keymap.Copy, StepListAction.Copy, request, canExecute);
        Bind(target, keymap.Paste, StepListAction.Paste, request, canExecute);
        Bind(target, keymap.Delete, StepListAction.Delete, request, canExecute);
        Bind(target, keymap.Undo, StepListAction.Undo, request, canExecute);
        Bind(target, keymap.Redo, StepListAction.Redo, request, canExecute);
    }

    private static MenuItem Item(string header, StepListAction action, Action<StepListAction> request)
    {
        var item = new MenuItem { Header = header, Tag = action };
        item.Click += (_, _) => request(action);
        return item;
    }

    private static void Bind(InputElement target, string gesture, StepListAction action, Action<StepListAction> request, Func<bool> canExecute)
        => target.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse(gesture), Command = new RelayCommand(() => request(action), canExecute) });
}
