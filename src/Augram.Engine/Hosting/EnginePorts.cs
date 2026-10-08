using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;

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

    /// <summary>Window lookup and activation for the command executor (M2); the null object knows no windows, so nothing resolves to an app group and nothing is activated.</summary>
    public IWindowSystem Windows { get; init; } = NullWindowSystem.Instance;

    /// <summary>What a <c>WindowOp</c> step acts through (M2); the null object declines every operation.</summary>
    public IWindowOperations WindowOperations { get; init; } = NullWindowOperations.Instance;

    /// <summary>What a <c>Run</c> step starts programs through (M2); the null object declines every launch.</summary>
    public IProcessLauncher ProcessLauncher { get; init; } = NullProcessLauncher.Instance;
    /// <summary>What the Display steps read and change (learnings 0002); the null object knows no displays, so they skip.</summary>
    public IDisplayModes DisplayModes { get; init; } = NullDisplayModes.Instance;

    /// <summary>The current mapping, read once per stroke by the executor; null means no executor at all (recognise and report only, as in M1).</summary>
    public Func<MappingDocument>? Mapping { get; init; }

    /// <summary>Asked before a recognised stroke or wheel tick is executed; true claims the event (the training popup consuming a stroke drawn over its canvas, F3/A6) and nothing fires.</summary>
    public Func<EngineEvent, bool>? Intercept { get; init; }
}
