using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.Engine.Hosting;

/// <summary>
/// What travels on the hook-to-worker channel. A struct so enqueuing allocates nothing beyond the
/// <see cref="CaptureEvent"/> (or <see cref="HoldRemapEvent"/>) itself. Setting changes ride the same queue so the worker
/// applies them in order with the events around them; that is what keeps the hook-side decision and the machine
/// in agreement across a stroke-button change (see <c>Input/SuppressionShadow</c>).
/// </summary>
/// <param name="Kind">What the message is.</param>
/// <param name="Event">For <see cref="MessageKind.Input"/>: the capture event.</param>
/// <param name="HookSuppressed">The hook's decision for the event (for a hold remap event: whether the hold remap swallowed it).</param>
/// <param name="Payload">A setting, a reason, a captured key, an action, or a <see cref="HoldRemapEvent"/>, by kind.</param>
/// <param name="Ordered">The hook counted this message as a pending replay (<c>HoldRemapShadow.PendingReplays</c>): the worker calls <c>InputGate.HoldReplayDone</c> once it has made its injections.</param>
/// <param name="App">For a hold key's press: the name of the app group whose plan it was claimed with, for the log.</param>
internal readonly record struct WorkerMessage(WorkerMessage.MessageKind Kind, CaptureEvent? Event = null, bool HookSuppressed = false, object? Payload = null, bool Ordered = false, string? App = null)
{
    internal enum MessageKind
    {
        Input,
        SetStrokeButton,
        SetThresholds,
        Reset,
        ButtonObserved,
        KeyCaptured,
        Notify,
        Hold,
        HoldReplay,
    }

    public static WorkerMessage Input(CaptureEvent e, bool hookSuppressed) => new(MessageKind.Input, e, hookSuppressed);

    /// <summary>A press with the button trigger outputs of the window it went down over (plan 0005 decision 9), in <see cref="Payload"/>.</summary>
    public static WorkerMessage Press(CaptureEvent.ButtonDown e, bool hookSuppressed, ButtonOutputs outputs) => new(MessageKind.Input, e, hookSuppressed, outputs);

    public static WorkerMessage StrokeButton(MouseButton button) => new(MessageKind.SetStrokeButton, Payload: button);

    public static WorkerMessage Thresholds(CaptureThresholds thresholds) => new(MessageKind.SetThresholds, Payload: thresholds);

    public static WorkerMessage Reset(string reason) => new(MessageKind.Reset, Payload: reason);

    /// <summary>A physical press seen while <c>EngineHost.CaptureNextButtonPress</c> was pending (F1 detect-to-assign).</summary>
    public static WorkerMessage ButtonObserved(MouseButton button) => new(MessageKind.ButtonObserved, Payload: button);

    /// <summary>A key event seen while a hotkey capture was armed (F5), for the capture's callback.</summary>
    public static WorkerMessage KeyCaptured(KeyCaptureEvent captured) => new(MessageKind.KeyCaptured, Payload: captured);

    /// <summary>Runs <paramref name="action"/> on the worker, in order with the messages around it (a capture's release notice after its key events).</summary>
    public static WorkerMessage Notify(Action action) => new(MessageKind.Notify, Payload: action);

    /// <summary>An event for the hold remap machine (F9) with the hook's hold decision: true when the hold remap swallowed it.</summary>
    public static WorkerMessage Hold(HoldRemapEvent e, bool hookSuppressed, bool ordered = false, string? app = null)
        => new(MessageKind.Hold, HookSuppressed: hookSuppressed, Payload: e, Ordered: ordered, App: app);

    /// <summary>A key event the hook swallowed only to keep the order behind a rollover's replays: the worker replays it as it was.</summary>
    public static WorkerMessage HoldReplay(HoldRemapEvent.Key key) => new(MessageKind.HoldReplay, HookSuppressed: true, Payload: key, Ordered: true);
}
