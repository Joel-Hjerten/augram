using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;

namespace Augram.Engine.Execution;

/// <summary>
/// One item on the worker-to-executor queue: what fired (a recognised gesture, a wheel tick or a click, with what the press
/// held), where the press started (the window under that point is the target, F5), and, for a gesture, the recognition log
/// entry the recognizer drafted but did not add. The executor completes the draft with the resolution (group, command, or why
/// nothing fired) and adds it; a wheel tick and a click have none. <see cref="Relay"/> is the click to hand to the app when
/// a click trigger fires nothing there. <see cref="EnqueuedAt"/> is a <see cref="System.Diagnostics.Stopwatch"/> timestamp
/// the executor stamps on enqueue, so "Command fired" can report release-to-done latency.
/// </summary>
internal sealed record ExecutionRequest(PressedTrigger Trigger, CapturePoint Start, RecognitionLogEntry? Draft)
{
    public long EnqueuedAt { get; init; }

    /// <summary>For a click trigger: the click relayed when nothing fires (none when a button joined the press).</summary>
    public ClickRelay? Relay { get; init; }

    /// <summary>For the log: the recognised gesture's name ("Shift + gesture '/Down'") when there is one, else the trigger's own phrase ("Right + wheel up").</summary>
    public string Describe()
    {
        var text = Trigger.Describe();
        return Trigger.Kind is Trigger.GestureTrigger && Draft is { TopMatches.Count: > 0 } draft
            ? text.Replace(Trigger.Kind.KindPhrase, $"gesture '{draft.TopMatches[0].Name}'", StringComparison.Ordinal)
            : text;
    }
}
