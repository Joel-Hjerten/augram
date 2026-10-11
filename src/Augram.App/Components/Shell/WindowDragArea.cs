using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Augram.App.Components.Shell;

/// <summary>
/// Makes a control's empty area act as the window's title bar (plan 0006 decision 5: the tabs live in the title bar, so
/// the window draws its own). A press that nothing inside handled moves the window (<see cref="Window.BeginMoveDrag"/>,
/// which keeps the system's snapping); a double press maximizes or restores it. Tabs and buttons inside mark their
/// presses handled, so they keep working. Set by the Default theme's shell template; harmless where the system already
/// treats the area as its caption, since the press then never reaches the control.
/// </summary>
public static class WindowDragArea
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(WindowDragArea));

    static WindowDragArea()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>((control, change) =>
        {
            control.PointerPressed -= OnPointerPressed;
            if (change.GetNewValue<bool>())
            {
                control.PointerPressed += OnPointerPressed;
            }
        });
    }

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || sender is not Control control || TopLevel.GetTopLevel(control) is not Window window
            || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2 && window.CanResize)
        {
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            window.BeginMoveDrag(e);
        }

        e.Handled = true;
    }
}
