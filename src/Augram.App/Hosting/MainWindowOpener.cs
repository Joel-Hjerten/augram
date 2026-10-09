using Augram.Core.Abstractions;
using Avalonia.Threading;

namespace Augram.App.Hosting;

/// <summary>
/// The App's <see cref="IAppWindow"/> (the Open app step's "This app (Augram)", Joel 2026-10-09): posts to the UI thread
/// whatever <see cref="Requested"/> holds, which <c>App</c> sets to the same open as a double click on the tray icon. Until
/// the window exists nothing is attached and <see cref="Open"/> answers false, so the step skips with its reason.
/// </summary>
public sealed class MainWindowOpener : IAppWindow
{
    private readonly Action<Action> _post;

    /// <param name="post">Runs an action on the UI thread; null is <see cref="Dispatcher.UIThread"/>'s Post.</param>
    public MainWindowOpener(Action<Action>? post = null)
    {
        _post = post ?? (action => Dispatcher.UIThread.Post(action));
    }

    /// <summary>Opens and focuses the main window; set once by <c>App</c> when the window exists.</summary>
    public Action? Requested { get; set; }

    public bool Open()
    {
        if (Requested is not { } open)
        {
            return false;
        }

        _post(open);
        return true;
    }
}
