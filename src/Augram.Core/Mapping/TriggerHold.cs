using System.Numerics;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Mapping;

/// <summary>
/// A trigger's "while holding" set (F1 "Triggers as combinations", Joel 2026-10-09; learnings 0003 §3–4): the mouse buttons
/// (the stroke button and physical ones) and the keys (Ctrl, Alt, Shift, Win; stored as <see cref="KeyModifiers"/>, named Ctrl,
/// Opt, Shift, Cmd on a Mac) held when it fires, and when they must have gone down (<see cref="Capture"/>). A gesture and a
/// click always hold the stroke button (it draws, it clicks); a wheel trigger holds it by default and may instead hold other
/// buttons, each of which is then an <em>anchor</em>: whichever of them goes down first owns the press and the others join it.
/// Matching is exact, as in SP.net: the set equals what the press held (<see cref="Matches"/>), never a subset.
/// </summary>
/// <param name="Buttons">The buttons held; <see cref="HeldButtons.Stroke"/> is the stroke button wherever the trigger runs.</param>
/// <param name="Keys">Ctrl, Alt, Shift and Win (Meta) held; left and right count as one.</param>
/// <param name="Capture">Before, After or Either, for the members besides the anchor.</param>
public sealed record TriggerHold(HeldButtons Buttons, KeyModifiers Keys = KeyModifiers.None, HoldCapture Capture = HoldCapture.Either)
{
    /// <summary>The stroke button alone: every trigger before combinations existed, and still the usual one.</summary>
    public static TriggerHold Default { get; } = new(HeldButtons.Stroke);

    /// <summary>The stroke button plus <paramref name="keys"/> and <paramref name="buttons"/>.</summary>
    public static TriggerHold WithStroke(KeyModifiers keys, HeldButtons buttons = HeldButtons.None, HoldCapture capture = HoldCapture.Either)
        => new(HeldButtons.Stroke | buttons, keys, capture);

    public bool HoldsStroke => (Buttons & HeldButtons.Stroke) != 0;

    /// <summary>The physical buttons named (not the stroke button).</summary>
    public HeldButtons Physical => Buttons & HeldButtonsExtensions.Physical;

    /// <summary>Something besides the anchor: keys, or buttons beyond the stroke button (or beyond one anchor without it).</summary>
    public bool HasMembers => Keys != KeyModifiers.None || (HoldsStroke ? Physical != HeldButtons.None : BitOperations.PopCount((uint)Physical) > 1);

    /// <summary>The stroke button alone (with any capture mode): what a plain trigger holds.</summary>
    public bool IsDefault => HoldsStroke && !HasMembers;

    /// <summary>True when some button can anchor it: the stroke button, or at least one other.</summary>
    public bool HasAnchor => HoldsStroke || Physical != HeldButtons.None;

    /// <summary>
    /// The stored form: unknown bits dropped, the stroke button added when <paramref name="strokeRequired"/> (a gesture or a
    /// click), and <see cref="HoldCapture.Either"/> when there is nothing besides the anchor.
    /// </summary>
    public TriggerHold Normalised(bool strokeRequired)
    {
        var hold = new TriggerHold(
            (Buttons & HeldButtonsExtensions.All) | (strokeRequired ? HeldButtons.Stroke : HeldButtons.None),
            Keys & PressHold.TrackedKeys,
            Capture);
        return hold.HasMembers ? hold : hold with { Capture = HoldCapture.Either };
    }

    /// <summary>The set on a machine whose stroke button is <paramref name="strokeButton"/>: that button named explicitly is the stroke button there.</summary>
    public TriggerHold ForStrokeButton(MouseButton strokeButton) => this with { Buttons = Buttons.ForStrokeButton(strokeButton) };

    /// <summary>
    /// SP.net's predicate (learnings 0003 §3.4), exact: the press's anchor is one of this set's anchors, and the other members
    /// S equal B ∪ A (Either), B with A empty (Before), or A with B empty (After).
    /// </summary>
    public bool Matches(PressHold press)
    {
        var hold = ForStrokeButton(press.StrokeButton);
        HeldButtons members;
        if (hold.HoldsStroke)
        {
            if (!press.IsStroke)
            {
                return false;
            }

            members = hold.Physical;
        }
        else
        {
            if (press.IsStroke || (hold.Physical & press.Anchor) == HeldButtons.None)
            {
                return false;
            }

            members = hold.Physical & ~press.Anchor;
        }

        var keys = hold.Keys & PressHold.TrackedKeys;
        var nothingBefore = press.Before == HeldButtons.None && press.BeforeKeys == KeyModifiers.None;
        var nothingAfter = press.After == HeldButtons.None && press.AfterKeys == KeyModifiers.None;
        return hold.Capture switch
        {
            HoldCapture.Before => members == press.Before && keys == press.BeforeKeys && nothingAfter,
            HoldCapture.After => members == press.After && keys == press.AfterKeys && nothingBefore,
            _ => members == (press.Before | press.After) && keys == (press.BeforeKeys | press.AfterKeys),
        };
    }

    /// <summary>
    /// The A7 rule for two triggers of the same kind (learnings 0003 §3.5, SP.net's conflict rule): the same buttons and keys,
    /// and capture modes that overlap (Either overlaps everything, Before–Before, After–After).
    /// </summary>
    public bool Overlaps(TriggerHold other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var a = Normalised(strokeRequired: false);
        var b = other.Normalised(strokeRequired: false);
        return a.Buttons == b.Buttons && a.Keys == b.Keys
            && (a.Capture == HoldCapture.Either || b.Capture == HoldCapture.Either || a.Capture == b.Capture);
    }

    /// <summary>
    /// "Ctrl + Shift + Right": the keys in HotkeyText's order and names for <paramref name="names"/>, then the buttons. The
    /// stroke button is named only when another button is too and the kind does not imply it (<paramref name="strokeImplied"/>
    /// for a gesture or a click). Empty for the stroke button alone.
    /// </summary>
    public string Describe(HostPlatform names, bool strokeImplied)
    {
        var parts = new List<string>(8);
        var keys = Keys & PressHold.TrackedKeys;
        if (keys != KeyModifiers.None)
        {
            parts.AddRange(HotkeyText.Format(keys, KeyCode.None, KeyModifiers.None, names).Split(HotkeyText.Separator));
        }

        if (HoldsStroke && !strokeImplied && Physical != HeldButtons.None)
        {
            parts.Add("Stroke");
        }

        parts.AddRange(Physical.Buttons().Select(button => button.ToString()));
        return string.Join(" + ", parts);
    }

    /// <summary>"keys held before the press", "keys pressed during the press"; null for Either or when there are no members.</summary>
    public string? DescribeCapture() => !HasMembers ? null : Capture switch
    {
        HoldCapture.Before => "held before the press",
        HoldCapture.After => "pressed during the press",
        _ => null,
    };
}
