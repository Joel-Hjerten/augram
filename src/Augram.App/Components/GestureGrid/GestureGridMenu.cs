using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.Components.GestureGrid;

/// <summary>Builds the context menu and key bindings of a <see cref="GestureGrid"/> (F5a); every entry ends in one <see cref="GestureGridAction"/>.</summary>
internal static class GestureGridMenu
{
    public static ContextMenu Build(Action<GestureGridAction> request)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("New gesture…", GestureGridAction.New, request));
        menu.Items.Add(Item("Add sample…", GestureGridAction.AddSample, request));
        menu.Items.Add(Item("Rename", GestureGridAction.Rename, request));
        menu.Items.Add(Item("Toggle active", GestureGridAction.ToggleActive, request));
        menu.Items.Add(Item("Delete", GestureGridAction.Delete, request));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Import from StrokesPlus.net…", GestureGridAction.Import, request));
        return menu;
    }

    /// <summary>
    /// Key bindings are evaluated from the focused element up before the key reaches it, so
    /// <paramref name="canExecute"/> must say no while a tile's name editor has focus, or Delete and
    /// Ctrl+Z would act on the gesture instead of the text.
    /// </summary>
    public static void BindKeys(InputElement target, Action<GestureGridAction> request, Func<bool> canExecute)
    {
        Bind(target, GestureGridKeymap.New, GestureGridAction.New, request, canExecute);
        Bind(target, GestureGridKeymap.Rename, GestureGridAction.Rename, request, canExecute);
        Bind(target, GestureGridKeymap.Delete, GestureGridAction.Delete, request, canExecute);
        Bind(target, GestureGridKeymap.Undo, GestureGridAction.Undo, request, canExecute);
        Bind(target, GestureGridKeymap.Redo, GestureGridAction.Redo, request, canExecute);
    }

    private static MenuItem Item(string header, GestureGridAction action, Action<GestureGridAction> request)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => request(action);
        return item;
    }

    private static void Bind(InputElement target, string gesture, GestureGridAction action, Action<GestureGridAction> request, Func<bool> canExecute)
        => target.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse(gesture), Command = new RelayCommand(() => request(action), canExecute) });
}
