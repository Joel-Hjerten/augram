using Avalonia.Controls;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;

namespace Augram.App.Components.ListEditing;

/// <summary>
/// The context menu and key bindings of an editable list (F5a), built from its entry table: every menu entry and every key
/// ends in one <typeparamref name="TAction"/>, which the list turns into its one <c>ActionRequested</c>. Each list keeps only
/// its table and its rules for what the selection allows (<c>CommandTreeMenu</c>, <c>StepListMenu</c>, <c>MasterDetailMenu</c>,
/// <c>GestureGridMenu</c>); the keys come from <see cref="EditKeymap"/>.
/// </summary>
internal static class ActionMenu<TAction>
    where TAction : struct, Enum
{
    /// <summary>A menu with one entry per row of <paramref name="entries"/>, in order; each entry's <c>Tag</c> is its action.</summary>
    public static ContextMenu Build(Action<TAction> request, params (string Header, TAction Action)[] entries)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entries);
        var menu = new ContextMenu();
        foreach (var (header, action) in entries)
        {
            var item = new MenuItem { Header = header, Tag = action };
            item.Click += (_, _) => request(action);
            menu.Items.Add(item);
        }

        return menu;
    }

    /// <summary>The menu's entries with their actions, for a list's <c>Refresh</c> rules.</summary>
    public static IEnumerable<(MenuItem Item, TAction Action)> Entries(ContextMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return menu.Items.OfType<MenuItem>().Where(item => item.Tag is TAction).Select(item => (item, (TAction)item.Tag!));
    }

    /// <summary>
    /// Binds each key of <paramref name="keys"/> (in order; a null key, such as <see cref="EditKeymap.RenameAlso"/> on macOS, is
    /// skipped) on <paramref name="target"/>. Key bindings are evaluated from the focused element up before the key reaches it,
    /// so <paramref name="canExecute"/> must say no while a row's name editor (or a step's form) has focus, or Delete and the
    /// undo key would act on the list instead of the text.
    /// </summary>
    public static void BindKeys(InputElement target, Action<TAction> request, Func<bool> canExecute, params (string? Gesture, TAction Action)[] keys)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var (gesture, action) in keys)
        {
            if (gesture is not null)
            {
                target.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse(gesture), Command = new RelayCommand(() => request(action), canExecute) });
            }
        }
    }
}
