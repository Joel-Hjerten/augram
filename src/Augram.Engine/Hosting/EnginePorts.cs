using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Engine.Hosting;

/// <summary>
/// Everything outside the engine that <see cref="EngineHost"/> talks to, resolved by the App's
/// composition root. The two required ports have no safe default; the rest default to null
/// objects so a test or a headless run needs only an input source and a simulator.
/// </summary>
public sealed record EnginePorts
{
    public required IInputSource Input { get; init; }

    public required IInputSimulator Simulator { get; init; }

    public IClock Clock { get; init; } = SystemClock.Instance;

    public IEventLog Log { get; init; } = NullEventLog.Instance;

    public IStrokeTrail Trail { get; init; } = NullStrokeTrail.Instance;

    public RecognitionLog RecognitionLog { get; init; } = new();

    /// <summary>When present, the host contributes hook and stroke health to it.</summary>
    public HealthRegistry? Health { get; init; }

    /// <summary>When absent, the hook watchdog relies on the source's own loss signal only.</summary>
    public ICursorProbe? CursorProbe { get; init; }

    public ISystemEvents? SystemEvents { get; init; }
}
