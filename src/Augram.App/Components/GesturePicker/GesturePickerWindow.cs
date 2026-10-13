using Augram.App.Components.ListEditing;
using Augram.App.Dialogs;

namespace Augram.App.Components.GesturePicker;

/// <summary>
/// The window around a <see cref="GesturePicker"/> (F3: a popup like the training window, owned by the main window), sized by
/// <see cref="DialogSize.Picker"/>. Code-only: its one job is to host the component. Escape closes it (<see cref="DialogWindow"/>),
/// which the presenter reads as Cancel, once: the picker's Cancel button does not fire as well. The find key
/// (<see cref="EditKeymap.Find"/>) focuses the picker's search box as the shell's does on a tab, and the box has the focus
/// as soon as the window opens, so a search can be typed at once (Joel, 2026-10-13).
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
        ActionMenu<GesturePickerAction>.BindKeys(this, _ => picker.FocusSearch(), static () => true, (EditKeymap.Current.Find, GesturePickerAction.Find));
        Opened += (_, _) => picker.FocusSearch();
    }

    public GesturePicker Picker { get; }
}
