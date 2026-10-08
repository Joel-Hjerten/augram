using Augram.App.Hosting;
using Augram.App.UsedBy;

namespace Augram.App.Views;

/// <summary>
/// Asks a second launch's take-over question (<see cref="InstanceStartup.ChooseAsync"/>) with the shared
/// <see cref="ConfirmDialog"/>, as a window of its own: nothing of this launch is up yet, so there is no main window to own it.
/// </summary>
public sealed class TakeOverPresenter : ITakeOverPresenter
{
    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel) =>
        new ConfirmDialog(title, message, confirmLabel).ShowAloneAsync();

    public Task InformAsync(string title, string message) =>
        new ConfirmDialog(title, message, "OK", cancelLabel: null).ShowAloneAsync();
}
