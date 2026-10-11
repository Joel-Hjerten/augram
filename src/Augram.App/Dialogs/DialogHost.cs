using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Augram.App.Dialogs;

/// <summary>
/// The one place that finds the main window and shows a dialog over it or on its own. A dialog is owned by the main window
/// while that window is visible; otherwise (a hidden launch, the window closed to the tray, macOS without a Dock icon) it is
/// a window of its own. Whether a dialog shown alone is activated is the caller's choice, kept per call site
/// (<c>activateWhenAlone</c>); the README lists who passes what. Static because the main window is the process's one fact,
/// read from the Avalonia lifetime: presenters and tests need no service for it, and headless tests have none (no owner).
/// </summary>
public static class DialogHost
{
    /// <summary>The lifetime's main window, shown or not; null before the root hands it over and in headless tests.</summary>
    public static Window? MainWindow => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    /// <summary>The main window while it is visible: what a dialog is owned by. Null means "show it alone".</summary>
    public static Window? Owner => MainWindow is { IsVisible: true } window ? window : null;

    /// <summary>
    /// Shows <paramref name="window"/> modal to <see cref="Owner"/> when there is one, otherwise as a window of its own
    /// (activated when <paramref name="activateWhenAlone"/>), and completes when it closes.
    /// </summary>
    public static async Task ShowModalAsync(Window window, bool activateWhenAlone)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (Owner is { } owner)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
            return;
        }

        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.TrySetResult();
        window.Show();
        if (activateWhenAlone)
        {
            window.Activate();
        }

        await closed.Task.ConfigureAwait(true);
    }

    /// <summary>Shows <paramref name="window"/> without waiting: owned by <see cref="Owner"/> when there is one, otherwise alone; activated either way.</summary>
    public static void Show(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (Owner is { } owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }

        window.Activate();
    }

    /// <summary>
    /// Shows <paramref name="window"/> as a window of its own whether or not a main window exists, centred on the screen and
    /// activated, and completes when it closes: for a question asked before this launch has a main window (the take-over).
    /// The task completes inside the window's <c>Closed</c> event, so a caller's synchronous continuation sees the answer at once.
    /// </summary>
    public static Task ShowAloneAsync(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var closed = new TaskCompletionSource();
        window.Closed += (_, _) => closed.TrySetResult();
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        window.Show();
        window.Activate();
        return closed.Task;
    }
}
