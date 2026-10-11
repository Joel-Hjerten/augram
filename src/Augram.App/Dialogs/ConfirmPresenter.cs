namespace Augram.App.Dialogs;

/// <summary>
/// Shows a <see cref="ConfirmDialog"/>. Over the main window (the default, what the tabs get): a question is modal to the
/// main window, and without a visible main window there is nothing to ask over and the answer is no, so a destructive action
/// never proceeds unasked; a message is modal to it, or shown alone and activated when it is hidden (the import's messages).
/// Alone (<see cref="Alone"/>, registered under <see cref="AloneKey"/>): every dialog is a window of its own, centred on the
/// screen and activated, since nothing of the launch is up yet (a second launch's take-over question,
/// <c>Hosting/InstanceStartup.ChooseAsync</c>).
/// </summary>
public sealed class ConfirmPresenter : IConfirmPresenter
{
    /// <summary>The service key of the take-over's presenter (<see cref="Alone"/>).</summary>
    public const string AloneKey = "alone";

    private readonly bool _alone;

    public ConfirmPresenter()
        : this(alone: false)
    {
    }

    private ConfirmPresenter(bool alone) => _alone = alone;

    /// <summary>The presenter that shows every dialog as a window of its own, whether or not a main window exists.</summary>
    public static ConfirmPresenter Alone() => new(alone: true);

    public async Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        if (_alone)
        {
            return await new ConfirmDialog(title, message, confirmLabel).ShowAloneAsync().ConfigureAwait(true);
        }

        // No dialog is built without an owner: a window, even unshown, takes a native window.
        if (DialogHost.Owner is not { } owner)
        {
            return false;
        }

        return await new ConfirmDialog(title, message, confirmLabel).ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    public Task InformAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message, "OK", cancelLabel: null);
        return _alone ? dialog.ShowAloneAsync() : DialogHost.ShowModalAsync(dialog, activateWhenAlone: true);
    }
}
