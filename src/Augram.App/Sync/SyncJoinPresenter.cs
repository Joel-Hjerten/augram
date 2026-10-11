using Augram.App.Dialogs;
using Augram.App.ViewModels;
using Augram.Core.Sync;

namespace Augram.App.Sync;

/// <summary>
/// Asks the join question through the shared <see cref="IFormDialogPresenter"/> over <see cref="SyncJoinViewModel.Declare"/>,
/// in a window wide enough for the choices (<see cref="DialogSize.Join"/>): modal to the main window when it is open, otherwise
/// a window of its own, activated (the start-up run usually finds the app in the tray, and the question must still be seen).
/// </summary>
public sealed class SyncJoinPresenter : ISyncJoinPresenter
{
    private readonly IFormDialogPresenter _dialogs;

    public SyncJoinPresenter(IFormDialogPresenter dialogs)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        _dialogs = dialogs;
    }

    /// <summary>The question as a form dialog request; the gallery shows the same.</summary>
    public static FormDialogRequest Request(SyncJoinViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return new FormDialogRequest(SyncJoinViewModel.Title, SyncJoinViewModel.ConfirmLabel, SyncJoinViewModel.Message, viewModel.Declare())
        {
            Width = DialogSize.Width(DialogSize.Join),
            ActivateWhenAlone = true,
        };
    }

    public async Task<SyncJoin?> ChooseAsync(IReadOnlyList<SyncMachineSummary> machines)
    {
        ArgumentNullException.ThrowIfNull(machines);
        var viewModel = new SyncJoinViewModel(machines);
        return await _dialogs.ShowAsync(Request(viewModel)).ConfigureAwait(true) ? viewModel.Choice : null;
    }
}
