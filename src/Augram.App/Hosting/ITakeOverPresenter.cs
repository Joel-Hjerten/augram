namespace Augram.App.Hosting;

/// <summary>
/// The two dialogs a second launch of a different build may show before anything else of it starts
/// (<see cref="InstanceStartup.ChooseAsync"/>). There is no main window yet, so each is a window of its own. Tests fake it.
/// </summary>
public interface ITakeOverPresenter
{
    /// <summary>True when the user pressed <paramref name="confirmLabel"/>; false for Cancel, Escape or closing the dialog.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel);

    /// <summary>A message with OK only; completes when the user closed it.</summary>
    Task InformAsync(string title, string message);
}
