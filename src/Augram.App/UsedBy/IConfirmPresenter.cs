namespace Augram.App.UsedBy;

/// <summary>
/// Asks the user a yes/no question before a destructive action (A9; the delete warning for a gesture
/// that commands use). Avalonia dialogs are awaitable only, so the answer is a task: a view model
/// awaits it on the UI thread, and a test's fake answers at once.
/// </summary>
public interface IConfirmPresenter
{
    /// <summary>True when the user pressed the <paramref name="confirmLabel"/> button; false for Cancel, Escape or closing the dialog.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);
}
