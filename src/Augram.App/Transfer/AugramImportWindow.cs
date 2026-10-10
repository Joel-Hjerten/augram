using Augram.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Transfer;

/// <summary>
/// The window around the import review (<see cref="AugramImportView"/>), sized like the sync conflict dialog and modal to the
/// main window. It closes when the view model finishes (imported or cancelled); Escape and the close box cancel, which
/// changes nothing. Code-only; its one job is to host the view.
/// </summary>
public sealed class AugramImportWindow : Window
{
    public AugramImportWindow(AugramImportViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        Title = AugramImportViewModel.Title;
        Width = 960;
        Height = 640;
        MinWidth = 640;
        MinHeight = 320;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        View = new AugramImportView(viewModel);
        Content = View;
        viewModel.Finished += (_, _) => Close();
    }

    public AugramImportView View { get; }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
