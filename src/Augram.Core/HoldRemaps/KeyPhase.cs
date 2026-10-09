namespace Augram.Core.HoldRemaps;

/// <summary>Which part of a key press an event is: its first down, an auto-repeat while it is held, or its release.</summary>
public enum KeyPhase
{
    Down,
    Repeat,
    Up,
}
