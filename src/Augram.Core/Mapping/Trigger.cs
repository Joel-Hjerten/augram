using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Mapping;

/// <summary>
/// What fires a <see cref="Command"/>: a recognised gesture, a wheel tick, a click, a button pressed while others are held, or
/// nothing yet, each with a "while holding" set (<see cref="Hold"/>, F1 "Triggers as combinations", Joel 2026-10-09):
/// "Shift + gesture", "Right + wheel up", "Shift + click" (SP.net's no-gesture action, fired at the stroke button's release),
/// "Right + Left" (<see cref="ButtonTrigger"/>, plan 0005: fired at Left's press while Right is held); or, for a command under a
/// hold remap (F9, plan 0002), the <see cref="InputTrigger"/> that fires it while the hold key is held. A closed set of records
/// with value equality; <see cref="Overlaps"/> is the A7 rule ("bound twice in this group", per hold remap for an input) and a
/// <see cref="PressedTrigger"/> says whether a press fires it (never an input). The constructor is private: the six nested
/// records are the whole set.
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

    /// <summary>
    /// A button trigger (plan 0005): <paramref name="button"/> pressed while <paramref name="hold"/> is held. The pressed button is
    /// never a member of its own set (dropped here); the set's stroke button and its anchors are <see cref="MappingRules"/>' to check.
    /// </summary>
    public static ButtonTrigger ForButton(MouseButton button, TriggerHold hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return new(button) { Hold = (hold with { Buttons = hold.Buttons & ~button.Flag() }).Normalised(strokeRequired: false) };
    }

    /// <summary>A click trigger: the stroke button released with <paramref name="hold"/>'s members held; nothing beyond the stroke button is <see cref="None"/>.</summary>
    public static Trigger ForClick(TriggerHold hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        var normalised = hold.Normalised(strokeRequired: true);
        return normalised.IsDefault ? None : new ClickTrigger { Hold = normalised };
    }

    /// <summary>The trigger of a command under a hold remap: <paramref name="input"/> in its stored form, holding nothing else.</summary>
    public static InputTrigger ForInput(HoldInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new(input.Normalised());
    }

    /// <summary>"Shift + this gesture", "Right + wheel up", "wheel up": the held set (named for <paramref name="names"/>, HotkeyText's default when null) and the kind.</summary>
    public string Describe(HostPlatform? names = null)
    {
        var held = Hold.Describe(names ?? HotkeyText.Names, NeedsStroke);
        return held.Length == 0 || !IsBound ? KindPhrase : $"{held} + {KindPhrase}";
    }

    /// <summary>
    /// The same kind with another set: a click or no trigger becomes whichever of the two <paramref name="hold"/> makes; an
    /// input holds nothing (its hold key is what is held), so it stays as it is.
    /// </summary>
    public Trigger WithHold(TriggerHold hold)
    {
        ArgumentNullException.ThrowIfNull(hold);
        return this switch
        {
            NoTrigger or ClickTrigger => ForClick(hold),
            InputTrigger input => ForInput(input.Input),
            ButtonTrigger button => ForButton(button.Button, hold),
            _ => this with { Hold = hold.Normalised(NeedsStroke) },
        };
    }

    /// <summary>The stored form: the hold normalised for the kind; a click holding nothing extra is no trigger; an input in its stored form.</summary>
    public Trigger Normalised() => WithHold(Hold);

    /// <summary>The same kind, whatever is held: the same gesture, the same wheel direction, a click, the same input, or both unbound.</summary>
    public bool IsSameKind(Trigger other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return this switch
        {
            GestureTrigger gesture => other is GestureTrigger o && o.GestureId == gesture.GestureId,
            WheelTrigger wheel => other is WheelTrigger o && o.Direction == wheel.Direction,
            ClickTrigger => other is ClickTrigger,
            InputTrigger input => other is InputTrigger o && o.Input == input.Input,
            ButtonTrigger button => other is ButtonTrigger o && o.Button == button.Button,
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

    /// <summary>
    /// <see cref="Button"/> pressed while the set is held (plan 0005, Joel 2026-10-10: Eyeris's loupe chord "hold Right, press
    /// Left" moved into Augram, the classic rocker). Fires at that press, from a press held back by one of the set's anchors; the
    /// set holds a button other than the stroke button and not the stroke button (<see cref="MappingRules"/>), and never the
    /// pressed button itself. A Remap step's key output is held while both are down (the engine plays it); ordinary steps run once
    /// at the press. "Right + Left".
    /// </summary>
    public sealed record ButtonTrigger(MouseButton Button) : Trigger
    {
        public override bool NeedsStroke => false;

        /// <summary>The pressed button's name: "Left", so the whole reads "Right + Left".</summary>
        public override string KindPhrase => Button.ToString();
    }

    /// <summary>The stroke button released without moving, with keys or buttons held (SP.net's no-gesture action, learnings 0003 §2.6).</summary>
    public sealed record ClickTrigger : Trigger
    {
        internal static ClickTrigger Observed { get; } = new();

        public override string KindPhrase => "click";
    }

    /// <summary>
    /// The input of a command under a hold remap (F9, plan 0002): a button, a set of buttons held together, a wheel direction
    /// or a key, while the hold key is held. It holds nothing besides (<see cref="Hold"/> stays the default and means
    /// nothing), converts to nothing on the other platform (inputs, like Remap outputs, are the same on both) and never
    /// matches a <see cref="PressedTrigger"/>: the resolver never fires it for a gesture or a wheel press; the hold remap does.
    /// Two clash (A7) only under the same hold remap.
    /// </summary>
    public sealed record InputTrigger(HoldInput Input) : Trigger
    {
        public override bool NeedsStroke => false;

        /// <summary>"Left + Right", "wheel up", "W".</summary>
        public override string KindPhrase => Input.Describe();
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
