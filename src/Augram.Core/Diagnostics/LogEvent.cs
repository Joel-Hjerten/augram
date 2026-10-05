namespace Augram.Core.Diagnostics;

/// <summary>
/// One structured log event. Immutable once built; the Engine moves it from the caller's thread
/// to the writer unchanged. <paramref name="Source"/> is a short lowercase component name
/// (<c>hook</c>, <c>capture</c>, <c>recognition</c>, <c>activate</c>, <c>steps</c>, <c>config</c>, <c>overlay</c>)
/// the Diagnostics tab filters on; <paramref name="Properties"/> carries the numbers a human or an
/// agent will want to read back (timings, counts, names), rendered <c>key=value</c> by the sinks.
/// </summary>
public sealed record LogEvent(
    DateTimeOffset Timestamp,
    EventLevel Level,
    string Source,
    string Message,
    IReadOnlyList<LogProperty>? Properties = null,
    Exception? Exception = null);
