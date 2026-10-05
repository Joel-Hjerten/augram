namespace Augram.Core.Abstractions;

/// <summary>
/// Time for the engine (ADR-0002 §2). <see cref="MonotonicMs"/> is the clock every
/// <c>Capture.CaptureEvent</c> is stamped with: milliseconds that never go backwards and
/// do not care about wall-clock changes. <see cref="UtcNow"/> is for log and ring entries.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Milliseconds on a monotonic clock. Only differences are meaningful.</summary>
    long MonotonicMs { get; }
}
