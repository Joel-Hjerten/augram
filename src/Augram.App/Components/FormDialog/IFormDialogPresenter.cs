namespace Augram.App.Components.FormDialog;

/// <summary>Shows a <see cref="FormDialogRequest"/> modally and answers true for the confirm button, false for Cancel, Escape or closing the window. View models ask here; tests substitute a fake.</summary>
public interface IFormDialogPresenter
{
    Task<bool> ShowAsync(FormDialogRequest request);
}
