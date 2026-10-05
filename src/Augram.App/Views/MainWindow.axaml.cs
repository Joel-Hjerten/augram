using Augram.App.Navigation;
using Augram.App.ViewModels;
using Avalonia.Controls;

namespace Augram.App.Views;

public sealed partial class MainWindow : Window
{
    /// <summary>For the XAML previewer and runtime loader only; the app resolves the other constructor.</summary>
    public MainWindow()
        : this(new MainWindowViewModel(new NavigationRegistry([])))
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Closing hides the window; the app keeps running in the tray (F7). Shutdown closes it for real.</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
