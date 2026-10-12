using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Augram.App.Hosting;

/// <summary>
/// Text input made only of control characters never reaches a text box (Joel, Mac, 0.13.11: Escape in an empty search box
/// typed a square). On macOS a key no handler marks handled is passed on as text when it has a key symbol, and Escape's is
/// U+001B, which a <see cref="TextBox"/> inserts as is (it strips only U+007F itself). One app-wide rule rather than a
/// per-box patch: registered once as a tunnelling class handler on <see cref="TextBox"/>, beside <see cref="ClickAwayFocus"/>,
/// so any such text is handled before the box sees it. Ordinary text, and keys the box handles as keys, are untouched.
/// </summary>
internal static class ControlCharacterInput
{
    private static int _registered;

    /// <summary>Once per process; later calls (another App instance, as tests make) do nothing.</summary>
    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            InputElement.TextInputEvent.AddClassHandler<TextBox>(OnTextInput, RoutingStrategies.Tunnel);
        }
    }

    /// <summary>True for text of control characters only (code points below U+0020, and U+007F); empty text is not.</summary>
    public static bool IsControlOnly(string? text) => text is { Length: > 0 } && text.All(c => c < ' ' || c == '\u007f');

    private static void OnTextInput(TextBox box, TextInputEventArgs e)
    {
        if (IsControlOnly(e.Text))
        {
            e.Handled = true;
        }
    }
}
