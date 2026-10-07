namespace Augram.Core.Abstractions;

/// <summary>
/// The semantic window operations a <c>WindowOp</c> step can ask for (F5). Platform-neutral by design
/// (Joel, 2026-10-07: "anything system related on both systems should map to each other"): each
/// platform adapter maps an operation to its own calls, and <see cref="IWindowOperations.Supports"/>
/// says when it has no equivalent, so the UI can mark the step rather than the step guessing.
/// The Windows ↔ macOS table lives in <c>Core/Steps/WindowOp/README.md</c>.
/// </summary>
public enum WindowOperation
{
    /// <summary>Ask the window to close, as its own close button would (never kill the process).</summary>
    Close,

    Minimize,

    /// <summary>Maximize, or restore when already maximized (macOS: zoom, see D6).</summary>
    MaximizeOrRestore,

    /// <summary>Flip the window's always-on-top state.</summary>
    ToggleAlwaysOnTop,

    /// <summary>Move the window to the centre of its monitor's work area, keeping its size.</summary>
    Center,

    /// <summary>Resize to the given <see cref="WindowSize"/>, keeping the top-left corner.</summary>
    SetSize,

    /// <summary>Restore if maximized, then fill the left half of the monitor's work area.</summary>
    SnapLeftHalf,

    /// <summary>Restore if maximized, then fill the right half of the monitor's work area.</summary>
    SnapRightHalf,
}
