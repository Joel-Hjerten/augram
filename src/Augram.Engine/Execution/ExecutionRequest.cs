using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;

namespace Augram.Engine.Execution;

/// <summary>
/// One item on the worker-to-executor queue: what fired (a recognised gesture or a wheel tick), where
/// the stroke started (the window under that point is the target, F5), and, for a gesture, the
/// recognition log entry the recognizer drafted but did not add. The executor completes the draft
/// with the resolution (group, command, or why nothing fired) and adds it; a wheel tick has none.
/// <see cref="EnqueuedAt"/> is a <see cref="System.Diagnostics.Stopwatch"/> timestamp the executor
/// stamps on enqueue, so "Command fired" can report release-to-done latency.
/// </summary>
internal sealed record ExecutionRequest(Trigger Trigger, CapturePoint Start, RecognitionLogEntry? Draft)
{
    public long EnqueuedAt { get; init; }

    /// <summary>For the log: the recognised gesture's name ("gesture '/Down'") when there is one, else the trigger's own phrase ("wheel up").</summary>
    public string Describe()
        => Trigger is Trigger.GestureTrigger && Draft is { TopMatches.Count: > 0 } draft
            ? $"gesture '{draft.TopMatches[0].Name}'"
            : Trigger.Describe();
}
