using Augram.App.Dialogs;
using Augram.App.ViewModels;

namespace Augram.App.Transfer;

/// <summary>
/// The window around the import review (<see cref="AugramImportView"/>), sized like the sync conflict dialog but taller and
/// modal to the main window. It closes when the view model finishes (imported or cancelled); Escape and the close box
/// cancel, which changes nothing. Code-only; its one job is to host the view.
/// </summary>
public sealed class AugramImportWindow : DialogWindow
{
    public AugramImportWindow(AugramImportViewModel viewModel)
        : base(DialogSize.Import)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Title = AugramImportViewModel.Title;
        CanResize = true;
        View = new AugramImportView(viewModel);
        Content = View;
        viewModel.Finished += (_, _) => Close();
    }

    public AugramImportView View { get; }
}
