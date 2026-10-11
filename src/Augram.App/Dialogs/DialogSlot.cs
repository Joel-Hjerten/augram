using Avalonia.Controls;

namespace Augram.App.Dialogs;

/// <summary>
/// One dialog of a kind at a time (the training popup, "Used by…"): <see cref="Show"/> closes the one on show, if any, and
/// shows the new one through <see cref="DialogHost.Show"/>. The slot forgets a window when it closes, however it closed.
/// </summary>
public sealed class DialogSlot
{
    /// <summary>The window on show, or null.</summary>
    public Window? Current { get; private set; }

    /// <summary>Closes the window on show, if any. A presenter calls it first when the close must come before other work (training cancels its session on close).</summary>
    public void Close() => Current?.Close();

    public void Show(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Close();
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(Current, window))
            {
                Current = null;
            }
        };
        Current = window;
        DialogHost.Show(window);
    }
}
