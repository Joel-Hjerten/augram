namespace Augram.App.Components.GesturePicker;

/// <summary>How the Select Gesture picker closed (F3).</summary>
public enum GesturePickerOutcome
{
    /// <summary>Cancel, Escape or the window's close button: the command's trigger stays as it was.</summary>
    Cancelled,

    /// <summary>"No Gesture": the command becomes trigger-less.</summary>
    NoGesture,

    /// <summary>OK or a double-click on a tile: the command is bound to that gesture.</summary>
    Selected,
}
