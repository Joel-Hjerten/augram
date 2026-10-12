using Augram.App.Dialogs;
using Augram.App.ViewModels;

namespace Augram.App.Import;

/// <summary>
/// The StrokesPlus.net import dialog (F8), sized by <see cref="DialogSize.StrokesPlusImport"/>: hosts <see cref="ImportView"/> and
/// closes when the view model finishes. Escape and the close box close it without applying, as Cancel does. Code-only; its one
/// job is to host the view.
/// </summary>
public sealed class ImportDialog : DialogWindow
{
    public ImportDialog(ImportViewModel viewModel)
        : base(DialogSize.StrokesPlusImport)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Title = ImportPresenter.Title;
        CanResize = true;
        DataContext = viewModel;
        Content = new ImportView();
        viewModel.Finished += (_, _) => Close();
    }
}
