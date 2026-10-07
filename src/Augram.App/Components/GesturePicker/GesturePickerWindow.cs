using Avalonia.Controls;

namespace Augram.App.Components.GesturePicker;

/// <summary>The window around a <see cref="GesturePicker"/> (F3: a popup like the training window, owned by the main window). Code-only: its one job is to host the component.</summary>
public sealed class GesturePickerWindow : Window
{
    public GesturePickerWindow(GesturePicker picker)
    {
        ArgumentNullException.ThrowIfNull(picker);
        Title = "Select Gesture";
        Width = 560;
        Height = 520;
        MinWidth = 360;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = picker;
    }
}
