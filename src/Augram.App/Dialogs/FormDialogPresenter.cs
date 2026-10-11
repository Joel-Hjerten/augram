using Augram.App.Components.Fields;
using Augram.App.Components.FormDialog;

namespace Augram.App.Dialogs;

/// <summary>Shows a <see cref="FormDialogRequest"/> as a <see cref="FormDialogWindow"/> through <see cref="DialogHost"/>: modal to the main window when it is visible, otherwise alone (activated if the request says so). Nothing here decides what the answer means.</summary>
public sealed class FormDialogPresenter : IFormDialogPresenter
{
    public async Task<bool> ShowAsync(FormDialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dialog = Build(request);
        var window = new FormDialogWindow(dialog, request.Title, request.Width);
        var confirmed = false;
        dialog.Confirmed += (_, _) =>
        {
            confirmed = true;
            window.Close();
        };
        dialog.Cancelled += (_, _) => window.Close();

        await DialogHost.ShowModalAsync(window, request.ActivateWhenAlone).ConfigureAwait(true);
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
