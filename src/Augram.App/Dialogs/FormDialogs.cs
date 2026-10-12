namespace Augram.App.Dialogs;

/// <summary>Shows an <see cref="IDialogForm"/> through the <see cref="IFormDialogPresenter"/>: the one way a form view model becomes a <see cref="FormDialogRequest"/>.</summary>
public static class FormDialogs
{
    /// <summary>The confirm label of a dialog that makes something new (an app group, an exclusion).</summary>
    public const string CreateLabel = "Create";

    /// <summary>The request for <paramref name="form"/>: its title, confirm label and form.</summary>
    public static FormDialogRequest Request(IDialogForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        return new FormDialogRequest(form.DialogTitle, form.DialogConfirmLabel, Screen: form.Declare());
    }

    /// <summary>Shows <paramref name="form"/> modally; true for the confirm button.</summary>
    public static Task<bool> ShowAsync(this IFormDialogPresenter dialogs, IDialogForm form)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        return dialogs.ShowAsync(Request(form));
    }
}
