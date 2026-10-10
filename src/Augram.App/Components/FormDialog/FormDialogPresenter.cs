using Augram.App.Components.Fields;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Augram.App.Components.FormDialog;

/// <summary>Shows a <see cref="FormDialogRequest"/> as a <see cref="FormDialogWindow"/>, modal to the main window when there is one. Nothing here decides what the answer means.</summary>
public sealed class FormDialogPresenter : IFormDialogPresenter
{
    public async Task<bool> ShowAsync(FormDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dialog = Build(request);
        var window = new FormDialogWindow(dialog, request.Title);
        var confirmed = false;
        dialog.Confirmed += (_, _) =>
        {
            confirmed = true;
            window.Close();
        };
        dialog.Cancelled += (_, _) => window.Close();

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            var closed = new TaskCompletionSource();
            window.Closed += (_, _) => closed.TrySetResult();
            window.Show();
            await closed.Task.ConfigureAwait(true);
        }

        return confirmed;
    }

    /// <summary>The dialog content a request describes, its confirm button following <see cref="FormDialogRequest.CanConfirm"/>; the gallery shows it without a window.</summary>
    public static FormDialog Build(FormDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dialog = new FormDialog { Message = request.Message, Screen = request.Screen, ConfirmLabel = request.ConfirmLabel };
        if (request.CanConfirm is { } canConfirm)
        {
            BindingObserver.Attach(dialog, canConfirm, value => dialog.CanConfirm = value);
        }

        return dialog;
    }
}
