using Augram.Core.Capture;

namespace Augram.Engine.Input;

/// <summary>What <see cref="OwnInjections"/> counts separately: each mouse button's releases (numbered as <see cref="MouseButton"/>), and vertical wheel notches.</summary>
public enum InjectionSlot
{
    Left = (int)MouseButton.Left,
    Middle = (int)MouseButton.Middle,
    Right = (int)MouseButton.Right,
    X1 = (int)MouseButton.X1,
    X2 = (int)MouseButton.X2,

    /// <summary>Vertical wheel notches (the Scroll step's, and a hold remap's wheel output).</summary>
    Wheel,
}
