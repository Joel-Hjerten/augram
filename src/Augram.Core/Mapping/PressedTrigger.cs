using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Mapping;

/// <summary>
/// What one press fired, as the engine observed it: the kind (the recognised gesture, the wheel direction of this tick, or
/// <see cref="Trigger.Click"/>) and what the press held (<see cref="PressHold"/>). <see cref="CommandResolver"/> asks
/// <see cref="Matches"/> of each command's trigger for this platform; matching is exact (learnings 0003 §3.4).
/// </summary>
public sealed record PressedTrigger(Trigger Kind, PressHold Hold)
{
    /// <summary>True when there is something to look up (not <see cref="Trigger.None"/>).</summary>
    public bool IsBound => Kind.IsBound;

    /// <summary>
    /// The press that <paramref name="trigger"/> describes, for callers that hold a configured trigger (tests, the training
    /// popup): its first anchor owns it, and its members were held before (Before) or pressed during it (After, Either).
    /// The stroke button is <paramref name="strokeButton"/>, or by default Right unless the trigger names Right itself.
    /// </summary>
    public static PressedTrigger Of(Trigger trigger, MouseButton? strokeButton = null)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var hold = trigger.Hold.Normalised(trigger.NeedsStroke && trigger.IsBound);
        var stroke = strokeButton
            ?? new[] { MouseButton.Right, MouseButton.Middle, MouseButton.Left, MouseButton.X1, MouseButton.X2 }.Where(button => !hold.Physical.Has(button)).DefaultIfEmpty(MouseButton.Right).First();
        var anchor = hold.HoldsStroke || hold.Physical == HeldButtons.None
            ? HeldButtons.Stroke
            : hold.Physical.Buttons().First().Flag();
        var members = hold.Physical & ~anchor;
        var press = hold.Capture == HoldCapture.Before
            ? new PressHold(anchor, stroke, members, hold.Keys)
            : new PressHold(anchor, stroke, After: members, AfterKeys: hold.Keys);
        return new PressedTrigger(trigger, press);
    }

    /// <summary>True when <paramref name="configured"/> fires for this press: the same kind, and its set matches exactly.</summary>
    public bool Matches(Trigger configured)
    {
        ArgumentNullException.ThrowIfNull(configured);
        return configured.IsBound && configured.IsSameKind(Kind) && configured.Hold.Matches(Hold);
    }

    /// <summary>"Shift + this gesture", "Right + wheel up", "Alt + click": what was held (anchor, Before and After together), then the kind.</summary>
    public string Describe(HostPlatform? names = null)
    {
        var held = new TriggerHold(
            (Hold.IsStroke ? HeldButtons.Stroke : Hold.Anchor) | Hold.Before | Hold.After,
            Hold.BeforeKeys | Hold.AfterKeys);
        var text = held.Describe(names ?? HotkeyText.Names, Kind.NeedsStroke);
        return text.Length == 0 || !Kind.IsBound ? Kind.KindPhrase : $"{text} + {Kind.KindPhrase}";
    }
}
