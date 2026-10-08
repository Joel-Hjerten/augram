using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Core.Steps;

/// <summary>
/// What a step may touch while it runs (ADR-0002 §4: "target window, gesture start point, services and
/// nothing else"). Built once per command by the executor; the same instance is handed to every step of
/// that command in order. <see cref="Target"/> is the window under the gesture start, already activated
/// per rule A20 when activation was needed; null when nothing was under the point.
/// </summary>
public sealed record StepExecutionContext(
    WindowIdentity? Target,
    CapturePoint Start,
    IWindowOperations Windows,
    IInputSimulator Input,
    IEventLog Log,
    CancellationToken Cancellation)
{
    /// <summary>True when the executor had to bring <see cref="Target"/> to the foreground; the settle delay (A8) applies before the first keystroke.</summary>
    public bool FocusMoved { get; init; }

    /// <summary>What a Run step starts programs through; the null object declines every launch.</summary>
    public IProcessLauncher Processes { get; init; } = NullProcessLauncher.Instance;
    /// <summary>What the Display steps read and change (learnings 0002); the null object knows no displays.</summary>
    public IDisplayModes Displays { get; init; } = NullDisplayModes.Instance;
}
