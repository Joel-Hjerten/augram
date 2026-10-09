using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Mapping;

/// <summary>
/// What fires a <see cref="Command"/>: a recognised gesture, a wheel tick, a click, or nothing yet, each with a "while
/// holding" set (<see cref="Hold"/>, F1 "Triggers as combinations", Joel 2026-10-09): "Shift + gesture", "Right + wheel up",
/// "Shift + click" (SP.net's no-gesture action, fired at the stroke button's release). A closed set of records with value
/// equality; <see cref="Overlaps"/> is the A7 rule ("bound twice in this group") and a <see cref="PressedTrigger"/> says
/// whether a press fires it. The constructor is private: the four nested records are the whole set.
/// </summary>
public abstract record Trigger
{
    private Trigger()
    {
    }

    /// <summary>A command that exists but is not bound yet (the gesture picker's "No Gesture").</summary>
    public static NoTrigger None => NoTrigger.Instance;

    /// <summary>The kind an observed click has (<see cref="PressedTrigger"/>); a configured click trigger comes from <see cref="ForClick"/>.</summary>
    public static ClickTrigger Click => ClickTrigger.Observed;

    /// <summary>The buttons and keys held when it fires; <see cref="TriggerHold.Default"/> (the stroke button alone) unless set.</summary>
    public TriggerHold Hold { get; init; } = TriggerHold.Default;

    /// <summary>True for a trigger the resolver can look up; false for <see cref="NoTrigger"/>.</summary>
    public bool IsBound => this is not NoTrigger;

    /// <summary>True for the kinds the stroke button always anchors: a gesture (it draws) and a click.</summary>
    public virtual bool NeedsStroke => true;

    /// <summary>The kind alone, for log lines: "this gesture", "wheel up", "click", "no trigger".</summary>
    public abstract string KindPhrase { get; }

    public static GestureTrigger ForGesture(GestureId gestureId, TriggerHold? hold = null)
        => new(gestureId) { Hold = (hold ?? TriggerHold.Default).Normalised(strokeRequired: true) };

    public static WheelTrigger ForWheel(WheelDirection direction, TriggerHold? hold = null)
        => new(direction) { Hold = (hold ?? TriggerHold.Default).Normalised(strokeRequired: false) };

    /// <summary>A click trigger: the stroke button released with <paramref name="hold"/>'s members held; nothing beyond the stroke button is <see cref="None"/>.</summary>
    public static Trigger ForClick(TriggerHold hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        var normalised = hold.Normalised(strokeRequired: true);
        return normalised.IsDefault ? None : new ClickTrigger { Hold = normalised };
    }

    /// <summary>"Shift + this gesture", "Right + wheel up", "wheel up": the held set (named for <paramref name="names"/>, HotkeyText's default when null) and the kind.</summary>
    public string Describe(HostPlatform? names = null)
    {
        var held = Hold.Describe(names ?? HotkeyText.Names, NeedsStroke);
        return held.Length == 0 || !IsBound ? KindPhrase : $"{held} + {KindPhrase}";
    }

    /// <summary>The same kind with another set: a click or no trigger becomes whichever of the two <paramref name="hold"/> makes.</summary>
    public Trigger WithHold(TriggerHold hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return this is NoTrigger or ClickTrigger ? ForClick(hold) : this with { Hold = hold.Normalised(NeedsStroke) };
    }

    /// <summary>The stored form: the hold normalised for the kind; a click holding nothing extra is no trigger.</summary>
    public Trigger Normalised() => WithHold(Hold);

    /// <summary>The same kind, whatever is held: the same gesture, the same wheel direction, a click, or both unbound.</summary>
    public bool IsSameKind(Trigger other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return this switch
        {
            GestureTrigger gesture => other is GestureTrigger o && o.GestureId == gesture.GestureId,
            WheelTrigger wheel => other is WheelTrigger o && o.Direction == wheel.Direction,
            ClickTrigger => other is ClickTrigger,
            _ => other is NoTrigger,
        };
    }

    /// <summary>A7 (learnings 0003 §3.5): both bound, the same kind, and sets that overlap (<see cref="TriggerHold.Overlaps"/>).</summary>
    public bool Overlaps(Trigger other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return IsBound && other.IsBound && IsSameKind(other) && Hold.Overlaps(other.Hold);
    }

    public sealed record GestureTrigger(GestureId GestureId) : Trigger
    {
        public override string KindPhrase => "this gesture";
    }

    public sealed record WheelTrigger(WheelDirection Direction) : Trigger
    {
        /// <summary>A wheel trigger may hold other buttons instead of the stroke button ("Right + wheel up").</summary>
        public override bool NeedsStroke => false;

        public override string KindPhrase => Direction == WheelDirection.Up ? "wheel up" : "wheel down";
    }

    /// <summary>The stroke button released without moving, with keys or buttons held (SP.net's no-gesture action, learnings 0003 §2.6).</summary>
    public sealed record ClickTrigger : Trigger
    {
        internal static ClickTrigger Observed { get; } = new();

        public override string KindPhrase => "click";
    }

    public sealed record NoTrigger : Trigger
    {
        private NoTrigger()
        {
        }

        public static NoTrigger Instance { get; } = new();

        public override string KindPhrase => "no trigger";
    }
}
