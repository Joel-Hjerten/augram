using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

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
            || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed || IsOnControlInside(control, e.Source))
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

    /// <summary>
    /// Whether the press landed on a tab, button or text box inside the area. Their presses bubble up through the area
    /// before their own control acts on them (a TabControl selects in its own handler, further up), so the area must leave
    /// them alone or no tab could be clicked (Joel's first run, 2026-10-11).
    /// </summary>
    private static bool IsOnControlInside(Control area, object? source)
    {
        for (var visual = source as Visual; visual is not null && !ReferenceEquals(visual, area); visual = visual.GetVisualParent())
        {
            if (visual is TabItem or Button or TextBox or ListBoxItem)
            {
                return true;
            }
        }

        return false;
    }
}
