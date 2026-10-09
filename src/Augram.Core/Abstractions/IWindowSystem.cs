namespace Augram.Core.Abstractions;

/// <summary>
/// Window lookup, identity and activation (ADR-0002 §2). Implemented by Platform.Windows / Platform.MacOS.
/// Every member may block for a few milliseconds and must be called from the engine worker, the command executor or the
/// ignore-list watch, never from a hook handler.
/// </summary>
public interface IWindowSystem
{
    /// <summary>The window under a screen point (physical pixels), or null when there is none.</summary>
    WindowIdentity? WindowAt(int x, int y);

    /// <summary>The current foreground window, or null when the OS reports none.</summary>
    WindowIdentity? Foreground();

    /// <summary>
    /// A cheap key for the window under the point: microseconds, no process lookup, so the ignore list's watch can ask on
    /// every pointer move and call <see cref="WindowAt"/> only when the key changes. The same window gives the same key
    /// while it lives; 0 means no window. Null: this platform has no cheap answer, and the caller asks
    /// <see cref="WindowAt"/> itself at a bounded rate.
    /// </summary>
    nint? WindowKeyAt(int x, int y);

    /// <summary>
    /// A cheap key for what has focus (the foreground window on Windows, the frontmost app on macOS), with the contract of
    /// <see cref="WindowKeyAt"/>: the ignore list's watch polls it and calls <see cref="Foreground"/> only when it changes.
    /// </summary>
    nint? ForegroundKey();

    /// <summary>
    /// Brings <paramref name="target"/>'s root to the foreground, applying rule A20 itself: when the target root
    /// already is the foreground root, or the target is the desktop, nothing happens and the result is
    /// <see cref="ActivationResult.NotNeeded"/>.
    /// </summary>
    ActivationResult Activate(WindowIdentity target);
}
