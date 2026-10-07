namespace Augram.Core.Abstractions;

/// <summary>
/// Acts on a top-level window (ADR-0002 §2; F5 window actions). Implemented by Platform.Windows /
/// Platform.MacOS; <see cref="NullWindowOperations"/> supports nothing. Every member may block for a
/// few milliseconds and is called from the command executor thread only, never from a hook handler
/// and never from the UI thread. Operations target the window's root (<see cref="WindowIdentity.RootHandle"/>).
/// </summary>
public interface IWindowOperations
{
    /// <summary>Which platform this adapter is, for the step's "authored on" marker and the not-supported reason.</summary>
    HostPlatform Platform { get; }

    /// <summary>Whether the platform has an equivalent for <paramref name="operation"/> at all (macOS has no always-on-top for arbitrary windows).</summary>
    bool Supports(WindowOperation operation);

    /// <summary><paramref name="size"/> is read for <see cref="WindowOperation.SetSize"/> only.</summary>
    WindowOperationResult Perform(WindowOperation operation, WindowIdentity window, WindowSize? size = null);
}
