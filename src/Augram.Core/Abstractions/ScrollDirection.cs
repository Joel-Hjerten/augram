namespace Augram.Core.Abstractions;

/// <summary>
/// Which way <see cref="IInputSimulator.Scroll"/> turns the wheel: up and down on the vertical wheel, left and right on the
/// horizontal one (a tilt wheel's side-scroll). The names are stable because the Scroll step writes them to the config.
/// </summary>
public enum ScrollDirection
{
    Up,
    Down,
    Left,
    Right,
}
