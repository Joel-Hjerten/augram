using Augram.Core.Capture;

namespace Augram.Engine.Hosting;

/// <summary>
/// What travels on the hook-to-worker channel. A struct so enqueuing allocates nothing beyond the
/// <see cref="CaptureEvent"/> itself. Setting changes ride the same queue so the worker applies them
/// in order with the events around them; that is what keeps the hook-side decision and the machine
/// in agreement across a stroke-button change (see <c>Input/SuppressionShadow</c>).
/// </summary>
internal readonly record struct WorkerMessage(WorkerMessage.MessageKind Kind, CaptureEvent? Event = null, bool HookSuppressed = false, object? Payload = null)
{
    internal enum MessageKind
    {
        Input,
        SetStrokeButton,
        SetThresholds,
        Reset,
    }

    public static WorkerMessage Input(CaptureEvent e, bool hookSuppressed) => new(MessageKind.Input, e, hookSuppressed);

    public static WorkerMessage StrokeButton(MouseButton button) => new(MessageKind.SetStrokeButton, Payload: button);

    public static WorkerMessage Thresholds(CaptureThresholds thresholds) => new(MessageKind.SetThresholds, Payload: thresholds);

    public static WorkerMessage Reset(string reason) => new(MessageKind.Reset, Payload: reason);
}
