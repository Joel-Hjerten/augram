using Augram.Core.Abstractions;

namespace Augram.Core.Steps.WindowOp;

/// <summary>
/// One window action on the window under the gesture start (F5: close, minimize, maximize/restore,
/// always on top, center, set size, snap halves). <paramref name="Size"/> is read for
/// <see cref="WindowOperation.SetSize"/> only and must be present for it; the type's reader insists,
/// the executor fails the step when a size is missing.
/// </summary>
public sealed record WindowOpStep(WindowOperation Operation, WindowSize? Size = null) : IStep
{
    public IStepType Type => WindowOpStepType.Instance;

    public string Summary => Operation switch
    {
        WindowOperation.Close => "Close window",
        WindowOperation.Minimize => "Minimize window",
        WindowOperation.MaximizeOrRestore => "Maximize or restore",
        WindowOperation.ToggleAlwaysOnTop => "Toggle always on top",
        WindowOperation.Center => "Center window",
        WindowOperation.SetSize => Size is { } size ? $"Set size {size.Width}×{size.Height}" : "Set size",
        WindowOperation.SnapLeftHalf => "Snap to left half",
        WindowOperation.SnapRightHalf => "Snap to right half",
        _ => Operation.ToString(),
    };
}
