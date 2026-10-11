using Augram.App.Navigation;
using Augram.App.Themes;
using Augram.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Augram.App.Views;

public sealed partial class MainWindow : Window
{
    /// <summary>Room the system keeps in the title bar: caption buttons on the right on Windows, traffic lights on the left on macOS.</summary>
    private static readonly Thickness WindowsTitleBarPadding = new(14, 0, 140, 0);
    private static readonly Thickness MacTitleBarPadding = new(80, 0, 14, 0);

    /// <summary>For the XAML previewer and runtime loader only; the app resolves the other constructor.</summary>
    public MainWindow()
        : this(new MainWindowViewModel(new NavigationRegistry([])))
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        if (ThemeSelector.DrawsTitleBar)
        {
            DrawOwnTitleBar();
        }
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

    /// <summary>
    /// The glass look puts the top-level tabs in the title bar (plan 0006 decision 5): the content extends under it, the
    /// system keeps its caption buttons (Windows) or traffic lights (macOS), and the shell's title bar moves the window
    /// (<see cref="Components.Shell.WindowDragArea"/>). The window's own title is not drawn by the system any more, so the
    /// shell shows it ("Augram (Dev) 0.10.3", requirements F7).
    /// </summary>
    private void DrawOwnTitleBar()
    {
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.PreferSystemChrome;
        ExtendClientAreaTitleBarHeightHint = this.TryFindResource("TitleBar.Height", ActualThemeVariant, out var height) && height is double h ? h : 46;
        Resources["TitleBar.Padding"] = OperatingSystem.IsMacOS() ? MacTitleBarPadding : WindowsTitleBarPadding;
    }
}
