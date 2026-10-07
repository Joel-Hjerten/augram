using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Augram.App.Views;

/// <summary>
/// A click outside the focused text box takes the focus away from it, in every window (Joel, 2026-10-07): without it a
/// click on empty space leaves the caret blinking and an edit that commits on leaving (a number field, an in-place
/// rename) waits for Enter. Registered once as a class handler on <see cref="Window"/>, tunnelling and seeing handled
/// presses too, so it runs before the clicked control: a click on another field or button then takes the focus as it
/// would anyway. A click inside the text box itself changes nothing.
/// </summary>
internal static class ClickAwayFocus
{
    private static int _registered;

    /// <summary>Once per process; later calls (another App instance, as tests make) do nothing.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            InputElement.PointerPressedEvent.AddClassHandler<Window>(OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private static void OnPressed(Window window, PointerPressedEventArgs e)
    {
        if (window.FocusManager?.GetFocusedElement() is not TextBox editor)
        {
            return;
        }

        if (e.Source is Visual source && (ReferenceEquals(source, editor) || editor.IsVisualAncestorOf(source)))
        {
            return;
        }

        window.FocusManager.ClearFocus();
    }
}
