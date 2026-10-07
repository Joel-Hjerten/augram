using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Augram.App.UsedBy;

/// <summary>
/// Asks through a <see cref="ConfirmDialog"/> modal to the main window. Without a visible main window
/// there is nothing to ask over, and the answer is no: a destructive action never proceeds unasked.
/// </summary>
public sealed class ConfirmPresenter : IConfirmPresenter
{
    public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null || !owner.IsVisible)
        {
            return false;
        }

        var dialog = new ConfirmDialog(title, message, confirmLabel);
        return await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
    }
}
