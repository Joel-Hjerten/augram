using Augram.Core.Abstractions;

namespace Augram.App.Components.WindowFinder;

/// <summary>A <see cref="WindowFinder"/> was released over another app's window: what the window system knows about it.</summary>
public sealed class WindowPickedEventArgs : EventArgs
{
    public WindowPickedEventArgs(WindowIdentity window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Window = window;
    }

    public WindowIdentity Window { get; }
}
