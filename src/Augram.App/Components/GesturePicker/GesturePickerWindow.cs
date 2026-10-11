using Augram.App.Dialogs;
using Avalonia.Controls;

namespace Augram.App.Components.GesturePicker;

/// <summary>The window around a <see cref="GesturePicker"/> (F3: a popup like the training window, owned by the main window), sized by <see cref="DialogSize.Picker"/>. Code-only: its one job is to host the component; Escape is the picker's Cancel button.</summary>
public sealed class GesturePickerWindow : Window
{
    public GesturePickerWindow(GesturePicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        Title = "Select Gesture";
        DialogSize.Apply(this, DialogSize.Picker);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = picker;
    }
}
