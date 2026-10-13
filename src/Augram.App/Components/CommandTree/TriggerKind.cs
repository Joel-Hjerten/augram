namespace Augram.App.Components.CommandTree;

/// <summary>
/// The trigger kinds the command header's dropdown offers (F5a; Joel 2026-10-09: Gesture / Wheel / No trigger, the wheel's
/// direction chosen beside it; plan 0005: Button, the pressed button chosen beside it, "Right + Left"; Joel 2026-10-13: Click,
/// the stroke button clicked without drawing, bare or while holding keys or buttons): the closed set behind <c>Trigger</c>, as
/// the UI names them. "No trigger" with keys or buttons ticked still becomes a click trigger.
/// </summary>
public enum TriggerKind
{
    None,
    Gesture,
    Wheel,
    Button,
    Click,
}
