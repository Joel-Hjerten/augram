using Augram.Core.Capture;

namespace Augram.Engine.Input;

/// <summary>
/// What <see cref="OwnInjections"/> counts separately: each mouse button's releases (numbered as <see cref="MouseButton"/>),
/// vertical wheel notches, each button's presses, and moves (plain and dragged, one slot).
/// </summary>
public enum InjectionSlot
{
    Left = (int)MouseButton.Left,
    Middle = (int)MouseButton.Middle,
    Right = (int)MouseButton.Right,
    X1 = (int)MouseButton.X1,
    X2 = (int)MouseButton.X2,

    /// <summary>Vertical wheel notches (the Scroll step's, and a hold remap's wheel output).</summary>
    Wheel,

    /// <summary>Presses, in <see cref="MouseButton"/> order (<see cref="OwnInjections.PressSlotOf"/>); claimed only where foreign input is taken (macOS).</summary>
    LeftPress,
    MiddlePress,
    RightPress,
    X1Press,
    X2Press,

    /// <summary>Moves and drags: a hold remap's re-posted drags and a chord's re-posted moves on macOS, and the simulator's <c>MoveTo</c>.</summary>
    Move,
}
