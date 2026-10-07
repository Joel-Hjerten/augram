using Augram.Core.Capture;
using Augram.Core.Gestures;

namespace Augram.Core.Mapping;

/// <summary>
/// What fires a <see cref="Command"/>: a recognised gesture, a wheel tick while the stroke button is
/// held (F5: the volume commands), or nothing yet. A closed set of records with value equality, so
/// the resolver finds a command by <c>==</c> and the rules can say "bound twice in this group" (A7).
/// The constructor is private: the three nested records are the whole set.
/// </summary>
public abstract record Trigger
{
    private Trigger()
    {
    }

    /// <summary>A command that exists but is not bound yet (the gesture picker's "No Gesture").</summary>
    public static NoTrigger None => NoTrigger.Instance;

    /// <summary>True for a trigger the resolver can look up; false for <see cref="NoTrigger"/>.</summary>
    public bool IsBound => this is not NoTrigger;

    public static GestureTrigger ForGesture(GestureId gestureId) => new(gestureId);

    public static WheelTrigger ForWheel(WheelDirection direction) => new(direction);

    /// <summary>One short phrase for the recognition log: "this gesture", "wheel up", "no trigger".</summary>
    public abstract string Describe();

    public sealed record GestureTrigger(GestureId GestureId) : Trigger
    {
        public override string Describe() => "this gesture";
    }

    public sealed record WheelTrigger(WheelDirection Direction) : Trigger
    {
        public override string Describe() => Direction == WheelDirection.Up ? "wheel up" : "wheel down";
    }

    public sealed record NoTrigger : Trigger
    {
        private NoTrigger()
        {
        }

        public static NoTrigger Instance { get; } = new();

        public override string Describe() => "no trigger";
    }
}
