using Augram.App.UsedBy;
using Augram.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Augram.App.Transfer;

/// <summary>
/// Shows the import's windows over the main window (modal when it is open, otherwise on their own, as the sync dialogs do):
/// the review in an <see cref="AugramImportWindow"/>, a message in the shared <see cref="ConfirmDialog"/> with one OK button.
/// </summary>
public sealed class AugramImportWindows : IAugramImportWindows
{
    public Task ShowReviewAsync(AugramImportViewModel review)
    {
        ArgumentNullException.ThrowIfNull(review);
        return ShowAsync(new AugramImportWindow(review));
    }

    public Task ShowMessageAsync(string title, string message)
        => ShowAsync(new ConfirmDialog(title, message, "OK", cancelLabel: null));

    private static async Task ShowAsync(Window window)
    {
        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
            return;
        }

        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();
        window.Activate();
        await closed.Task.ConfigureAwait(true);
    }
}
