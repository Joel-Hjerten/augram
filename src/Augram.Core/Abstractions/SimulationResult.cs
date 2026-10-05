namespace Augram.Core.Abstractions;

/// <summary>Outcome of one <see cref="IInputSimulator"/> call, for the step log (N4) and the step's result code.</summary>
public enum SimulationResult
{
    Success,

    /// <summary>The OS or the input library refused the injection.</summary>
    Failed,

    /// <summary>A key or character has no mapping on this platform or layout; nothing was injected for it.</summary>
    Unsupported,
}
