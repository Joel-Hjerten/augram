namespace Augram.Core.Diagnostics;

/// <summary>
/// One structured log event. Immutable once built; the Engine moves it from the caller's thread
/// to the writer unchanged. <paramref name="Source"/> is one of <see cref="LogSources"/>, the short
/// lowercase component names the Diagnostics tab filters on; <paramref name="Properties"/> carries the numbers a human or an
/// agent will want to read back (timings, counts, names), rendered <c>key=value</c> by the sinks.
/// </summary>
public sealed record LogEvent(
    DateTimeOffset Timestamp,
    EventLevel Level,
    string Source,
    string Message,
    IReadOnlyList<LogProperty>? Properties = null,
    Exception? Exception = null);
