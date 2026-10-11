using Augram.App.Dialogs;
using Augram.Core.Abstractions;
using Avalonia.Controls;

namespace Augram.App.Hosting;

/// <summary>
/// Shows the main window: <see cref="ShowNow"/> on the UI thread (the tray's Open and double click, a second launch, a macOS
/// reopen), and as the App's <see cref="IAppWindow"/> (the Open app step's "This app (Augram)", Joel 2026-10-09) through
/// <see cref="Open"/>, which posts it to the UI thread. Until <c>App</c> has made the window and called <see cref="Attach"/>,
/// <see cref="Open"/> answers false, so the step skips with its reason.
/// </summary>
public sealed class MainWindowOpener : IAppWindow
{
    private readonly Action<Action> _post;
    private volatile bool _attached;

    /// <param name="post">Runs an action on the UI thread; null is <see cref="UiThread.Post"/>.</param>
    public MainWindowOpener(Action<Action>? post = null)
    {
        _post = post ?? UiThread.Post;
    }

    /// <summary>Called once by <c>App</c> when the main window exists.</summary>
    public void Attach() => _attached = true;

    public bool Open()
    {
        if (!_attached)
        {
            return false;
        }

        _post(ShowNow);
        return true;
    }

    /// <summary>UI thread: shows the main window, restores it when minimized and activates it; nothing before there is one.</summary>
    public void ShowNow()
    {
        if (DialogHost.MainWindow is not { } window)
        {
            return;
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }
}
