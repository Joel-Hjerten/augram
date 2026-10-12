using Augram.App.Dialogs;

namespace Augram.App.Components.GesturePicker;

/// <summary>
/// The window around a <see cref="GesturePicker"/> (F3: a popup like the training window, owned by the main window), sized by
/// <see cref="DialogSize.Picker"/>. Code-only: its one job is to host the component. Escape closes it (<see cref="DialogWindow"/>),
/// which the presenter reads as Cancel, once: the picker's Cancel button does not fire as well.
/// </summary>
public sealed class GesturePickerWindow : DialogWindow
{
    public GesturePickerWindow(GesturePicker picker)
        : base(DialogSize.Picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        Title = "Select Gesture";
        Picker = picker;
        Content = picker;
    }

    public GesturePicker Picker { get; }
}
