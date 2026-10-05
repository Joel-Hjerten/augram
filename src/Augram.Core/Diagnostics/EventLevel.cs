namespace Augram.Core.Diagnostics;

/// <summary>Severity of a <see cref="LogEvent"/>, least to most severe. Comparable: a minimum level of <see cref="Info"/> admits Info, Warning and Error.</summary>
public enum EventLevel
{
    /// <summary>Per-event hook traces (N4 "verbose"). Off by default; the only level the hook thread emits at.</summary>
    Trace,

    Debug,

    /// <summary>Everything N4 says is always logged: hook lifecycle, strokes, recognition, activation, steps, config.</summary>
    Info,

    Warning,

    Error,
}
