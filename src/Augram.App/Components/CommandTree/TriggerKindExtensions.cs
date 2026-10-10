using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The one place a <see cref="Trigger"/> becomes a <see cref="TriggerKind"/> and the words the header, the row badge and the
/// summary show for it: "Gesture" / "Wheel" / "No trigger" in the dropdown (Joel's order), "Right + wheel up", "Shift + Undo",
/// "Shift + click" for a whole trigger, the hint beside the kind, and the trigger's words in the header's note on a draft
/// ("Stroke button + wheel up", "Change the direction, a button or a key.").
/// </summary>
public static class TriggerKindExtensions
{
    /// <summary>The dropdown's entries, in Joel's order (2026-10-09).</summary>
    public static IReadOnlyList<TriggerKind> All { get; } = [TriggerKind.Gesture, TriggerKind.Wheel, TriggerKind.None];

    /// <summary>The wheel direction choice beside the kind, in this order.</summary>
    public static IReadOnlyList<WheelDirection> Directions { get; } = [WheelDirection.Up, WheelDirection.Down];

    /// <summary>The capture-mode choice, in this order (learnings 0003 §3.1: Either by default).</summary>
    public static IReadOnlyList<HoldCapture> Captures { get; } = [HoldCapture.Either, HoldCapture.Before, HoldCapture.After];

    public static string Label(this TriggerKind kind) => kind switch
    {
        TriggerKind.Gesture => "Gesture",
        TriggerKind.Wheel => "Wheel",
        _ => "No trigger",
    };

    public static string Label(this WheelDirection direction) => direction == WheelDirection.Up ? "Up" : "Down";

    /// <summary>When the held keys and buttons must have gone down, relative to the stroke button (or the anchor).</summary>
    public static string Label(this HoldCapture capture) => capture switch
    {
        HoldCapture.Before => "Held before the press",
        HoldCapture.After => "Pressed during the press",
        _ => "Before or during",
    };

    public static TriggerKind KindOf(Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return trigger switch
        {
            Trigger.GestureTrigger => TriggerKind.Gesture,
            Trigger.WheelTrigger => TriggerKind.Wheel,
            _ => TriggerKind.None,
        };
    }

    /// <summary>
    /// The whole trigger as a row and a summary show it: the held keys and buttons (named for <paramref name="names"/>), then
    /// <paramref name="gestureText"/> for a gesture, "wheel up", "click"; a hold remap command's input ("Left + Right",
    /// <see cref="InputKindExtensions.Text"/>); "No trigger" when unbound.
    /// </summary>
    public static string Text(Trigger trigger, string gestureText, HostPlatform names)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var held = trigger.IsBound ? trigger.Hold.Describe(names, trigger.NeedsStroke) : string.Empty;
        return trigger switch
        {
            Trigger.GestureTrigger => held.Length == 0 ? gestureText : $"{held} + {gestureText}",
            Trigger.InputTrigger => InputKindExtensions.Text(trigger),
            Trigger.NoTrigger => "No trigger",
            _ => held.Length == 0 ? Capitalised(trigger.KindPhrase) : $"{held} + {trigger.KindPhrase}",
        };
    }

    /// <summary>
    /// The whole trigger inside a sentence (the header's note on a trigger not saved yet): <see cref="Text"/>, except that a wheel
    /// trigger names the stroke button it holds, since for a wheel that box is a choice ("Stroke button + wheel up", "Stroke
    /// button + Shift + wheel up"), and a gesture is called one ("Shift + gesture 'Up'").
    /// </summary>
    public static string Phrase(Trigger trigger, string gestureName, HostPlatform names)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        if (trigger is not Trigger.WheelTrigger || !trigger.Hold.HoldsStroke)
        {
            return Text(trigger, $"gesture '{gestureName}'", names);
        }

        var held = trigger.Hold.Describe(names, strokeImplied: true);
        return held.Length == 0 ? $"Stroke button + {trigger.KindPhrase}" : $"Stroke button + {held} + {trigger.KindPhrase}";
    }

    /// <summary>What the user can change to get past a trigger another command already uses: the gesture or the direction, a button, a key.</summary>
    public static string FixHint(Trigger trigger) => trigger switch
    {
        Trigger.WheelTrigger => "Change the direction, a button or a key.",
        Trigger.GestureTrigger => "Change the gesture, a button or a key.",
        Trigger.InputTrigger => "Choose another input.",
        _ => "Change a button or a key.",
    };

    /// <summary>
    /// One line on how the trigger fires, for the kinds a user cannot guess from the name: the wheel and the click work only while
    /// the buttons are held. Null for a plain gesture and for no trigger.
    /// </summary>
    public static string? Hint(Trigger trigger, HostPlatform names)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var holding = Holding(trigger.Hold, names);
        return trigger switch
        {
            Trigger.WheelTrigger wheel => $"Hold {holding} and turn the mouse wheel {(wheel.Direction == WheelDirection.Up ? "up" : "down")}; every notch fires.",
            Trigger.ClickTrigger => $"Click the stroke button while holding {holding}; with nothing else bound here the click goes to the app, keys and all.",
            _ => null,
        };
    }

    /// <summary>
    /// The anchors other than the stroke button (Joel, 2026-10-09: per app): their clicks in this group wait for release or a
    /// move. "Right clicks in Chrome wait until you release or move." / "…in every app" for Global; null when none.
    /// </summary>
    public static string? AnchorWarning(Trigger trigger, AppGroup group)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(group);
        if (!trigger.IsBound || trigger.Hold.HoldsStroke || trigger.Hold.Physical == HeldButtons.None)
        {
            return null;
        }

        var buttons = string.Join(" and ", trigger.Hold.Physical.Buttons().Select(button => button.ToString()));
        var where = group.IsGlobal ? "in every app" : $"in {group.Name}";
        return $"{buttons} clicks {where} wait until you release or move.";
    }

    /// <summary>"the stroke button", "Right", "Shift and the stroke button", "Ctrl, Alt and Right".</summary>
    private static string Holding(TriggerHold hold, HostPlatform names)
    {
        var parts = new List<string>(8);
        var keys = (hold with { Buttons = HeldButtons.None }).Describe(names, strokeImplied: true);
        parts.AddRange(keys.Split(" + ", StringSplitOptions.RemoveEmptyEntries));
        if (hold.HoldsStroke)
        {
            parts.Add("the stroke button");
        }

        parts.AddRange(hold.Physical.Buttons().Select(button => button.ToString()));
        return parts.Count switch
        {
            0 => "the stroke button",
            1 => parts[0],
            _ => $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}",
        };
    }

    private static string Capitalised(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
