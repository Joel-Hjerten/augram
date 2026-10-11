using Augram.App.Declarations;

namespace Augram.App.Dialogs;

/// <summary>
/// What a <see cref="Components.FormDialog.FormDialog"/> shows: a title, the confirm button's label ("Create", "Delete"),
/// an optional message line (a confirmation question) and an optional declared form (the app group
/// form). Either part may be absent; a confirmation is a message alone.
/// </summary>
public sealed record FormDialogRequest(string Title, string ConfirmLabel, string? Message = null, FormScreen? Screen = null)
{
    /// <summary>Enables the confirm button while it reads true (the export dialog: something to export); null always enables it.</summary>
    public IValueBinding<bool>? CanConfirm { get; init; }

    /// <summary>The window's width; null is the form dialog's (<see cref="DialogSize.Form"/>). Sync's join question is wider (<see cref="DialogSize.Join"/>).</summary>
    public double? Width { get; init; }

    /// <summary>
    /// Activates the window when it is shown alone (no visible main window to own it). False by default, as the tabs' forms
    /// only open from the main window; sync's join question sets it, since the start-up run usually finds the app in the tray.
    /// </summary>
    public bool ActivateWhenAlone { get; init; }
}
