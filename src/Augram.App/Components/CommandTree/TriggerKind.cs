namespace Augram.App.Components.CommandTree;

/// <summary>
/// The trigger kinds the command header's dropdown offers (F5a; Joel 2026-10-09: Gesture / Wheel / No trigger, the wheel's
/// direction chosen beside it): the closed set behind <c>Trigger</c>, as the UI names them. "No trigger" with keys or buttons
/// held is a click trigger (the stroke button clicked while holding them).
/// </summary>
public enum TriggerKind
{
    None,
    Gesture,
    Wheel,
}
