using Augram.App.Declarations;

namespace Augram.App.Dialogs;

/// <summary>
/// A form view model shown in the shared form dialog: its title, its confirm button's label and its declared form. The new app
/// group and new exclusion forms, the Not in, Also in and Allowed for check lists, and the export dialog. A caller shows it with
/// <see cref="FormDialogs.ShowAsync"/>, or builds the request with <see cref="FormDialogs.Request"/> to add to it (export's
/// <see cref="FormDialogRequest.CanConfirm"/>); what Save stores stays with the caller.
/// </summary>
public interface IDialogForm
{
    /// <summary>The dialog's title ("Not in", "New app group").</summary>
    string DialogTitle { get; }

    /// <summary>The confirm button's label ("Save", "Create").</summary>
    string DialogConfirmLabel { get; }

    /// <summary>The form, declared afresh for each dialog.</summary>
    FormScreen Declare();
}
