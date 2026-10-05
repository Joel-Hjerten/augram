using CoreButton = Augram.Core.Capture.MouseButton;
using HookButton = SharpHook.Data.MouseButton;

namespace Augram.Engine.Input;

/// <summary>
/// SharpHook numbers buttons libuiohook's way (1 left, 2 right, 3 middle), not Windows' way.
/// This is the only place that knows it; Core sees <see cref="CoreButton"/> only.
/// </summary>
public static class MouseButtonMap
{
    public static bool TryToCore(HookButton button, out CoreButton core)
    {
        switch (button)
        {
            case HookButton.Button1:
                core = CoreButton.Left;
                return true;
            case HookButton.Button2:
                core = CoreButton.Right;
                return true;
            case HookButton.Button3:
                core = CoreButton.Middle;
                return true;
            case HookButton.Button4:
                core = CoreButton.X1;
                return true;
            case HookButton.Button5:
                core = CoreButton.X2;
                return true;
            default:
                core = default;
                return false;
        }
    }

    public static HookButton ToHook(CoreButton button) => button switch
    {
        CoreButton.Left => HookButton.Button1,
        CoreButton.Right => HookButton.Button2,
        CoreButton.Middle => HookButton.Button3,
        CoreButton.X1 => HookButton.Button4,
        CoreButton.X2 => HookButton.Button5,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown mouse button."),
    };
}
