namespace Augram.App.Dialogs;

/// <summary>
/// Asks the user a yes/no question before a destructive action (A9; the delete warning for a gesture that commands use), or
/// tells them something with OK only. Avalonia dialogs are awaitable only, so the answer is a task: a view model awaits it
/// on the UI thread, and a test's fake answers at once. Two registrations (<see cref="ConfirmPresenter"/>): the tabs' one over
/// the main window, and the take-over's one (keyed <see cref="ConfirmPresenter.AloneKey"/>) that shows every dialog alone.
/// </summary>
public interface IConfirmPresenter
{
    /// <summary>True when the user pressed the <paramref name="confirmLabel"/> button; false for Cancel, Escape or closing the dialog.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);

    /// <summary>A message with OK only; completes when the user closed it.</summary>
    Task InformAsync(string title, string message);
}
