using Augram.Core.Gestures;

namespace Augram.App.Components.GesturePicker;

/// <summary>Opens the Select Gesture picker (F3) and answers when it closes. The Commands view model asks here; tests substitute a fake.</summary>
public interface IGesturePickerPresenter
{
    /// <param name="current">The gesture the command is bound to now, preselected; null when none.</param>
    Task<GesturePickerResult> PickAsync(GestureId? current);
}
