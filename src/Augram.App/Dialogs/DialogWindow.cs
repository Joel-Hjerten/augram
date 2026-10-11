using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Dialogs;

/// <summary>
/// The window around a dialog: sized from its <see cref="DialogSize"/> tokens, centred on its owner, and closed by Escape.
/// Escape is handled here and only here (marked handled, so a cancel button's own Escape never fires as well); closing
/// is every dialog's "cancel", so the presenter reads the answer the dialog recorded before it closed. Code-only.
/// </summary>
public abstract class DialogWindow : Window
{
    protected DialogWindow(string size)
    {
        DialogSize.Apply(this, size);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
