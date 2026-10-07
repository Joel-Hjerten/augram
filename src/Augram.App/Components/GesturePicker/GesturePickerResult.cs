using Augram.Core.Gestures;

namespace Augram.App.Components.GesturePicker;

/// <summary>What the Select Gesture picker answered (F3): an outcome and, for <see cref="GesturePickerOutcome.Selected"/>, the gesture.</summary>
public sealed record GesturePickerResult(GesturePickerOutcome Outcome, GestureId? GestureId = null)
{
    public static GesturePickerResult Cancelled { get; } = new(GesturePickerOutcome.Cancelled);

    public static GesturePickerResult NoGesture { get; } = new(GesturePickerOutcome.NoGesture);

    public static GesturePickerResult Selected(GestureId id) => new(GesturePickerOutcome.Selected, id);
}
