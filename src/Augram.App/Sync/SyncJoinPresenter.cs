using Augram.App.Components.FormDialog;
using Augram.App.ViewModels;
using Augram.Core.Sync;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Augram.App.Sync;

/// <summary>
/// Shows the join question as the shared <see cref="FormDialog"/> over <see cref="SyncJoinViewModel.Declare"/>, in a
/// <see cref="FormDialogWindow"/> wide enough for the choices: modal to the main window when it is open, otherwise a
/// window of its own (the start-up run usually finds the app in the tray, and the question must still be seen).
/// </summary>
public sealed class SyncJoinPresenter : ISyncJoinPresenter
{
    public const double WindowWidth = 680;

    public async Task<SyncJoin?> ChooseAsync(IReadOnlyList<SyncMachineSummary> machines)
    {
        ArgumentNullException.ThrowIfNull(machines);
        var viewModel = new SyncJoinViewModel(machines);
        var dialog = new FormDialog { Message = SyncJoinViewModel.Message, Screen = viewModel.Declare(), ConfirmLabel = SyncJoinViewModel.ConfirmLabel };
        var window = new FormDialogWindow(dialog, SyncJoinViewModel.Title) { Width = WindowWidth };
        var confirmed = false;
        dialog.Confirmed += (_, _) =>
        {
            confirmed = true;
            window.Close();
        };
        dialog.Cancelled += (_, _) => window.Close();

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            var closed = new TaskCompletionSource();
            window.Closed += (_, _) => closed.TrySetResult();
            window.Show();
            window.Activate();
            await closed.Task.ConfigureAwait(true);
        }

        return confirmed ? viewModel.Choice : null;
    }
}
