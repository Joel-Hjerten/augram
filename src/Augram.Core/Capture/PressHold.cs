using Augram.Core.Abstractions;

namespace Augram.Core.Capture;

/// <summary>
/// What one press held, as the <see cref="CaptureStateMachine"/> saw it (learnings 0003 §2.1, §4.1): the anchor that owns the
/// press (<see cref="HeldButtons.Stroke"/>, or the one physical button held back), the keys and buttons already held when it
/// went down (<em>Before</em>, B) and those pressed while it was held (<em>After</em>, A; their downs were swallowed). Left and
/// right keys count as one. A wheel trigger sees the sets frozen at its first tick. Matched against a command's trigger by
/// <c>Mapping.TriggerHold.Matches</c>.
/// </summary>
/// <param name="Anchor">The stroke button, or the physical button that owns the press.</param>
/// <param name="StrokeButton">The stroke button when the press began, so a trigger naming that button explicitly reads as the stroke button.</param>
/// <param name="Before">Physical buttons already down when the anchor went down (their clicks reached the app).</param>
/// <param name="BeforeKeys">Ctrl, Alt, Shift, Win held when the anchor went down.</param>
/// <param name="After">Physical buttons pressed while the anchor was held and swallowed.</param>
/// <param name="AfterKeys">Ctrl, Alt, Shift, Win pressed while the anchor was held and swallowed.</param>
public readonly record struct PressHold(
    HeldButtons Anchor,
    MouseButton StrokeButton,
    HeldButtons Before = HeldButtons.None,
    KeyModifiers BeforeKeys = KeyModifiers.None,
    HeldButtons After = HeldButtons.None,
    KeyModifiers AfterKeys = KeyModifiers.None)
{
    /// <summary>Ctrl, Alt, Shift and Win: the keys a press records.</summary>
    public const KeyModifiers TrackedKeys = KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta;

    /// <summary>A plain press of the stroke button: nothing held before, nothing pressed during.</summary>
    public static PressHold OfStroke(MouseButton strokeButton) => new(HeldButtons.Stroke, strokeButton);

    /// <summary>Nothing besides the anchor: a plain click, stroke or wheel.</summary>
    public bool IsEmpty => Before == HeldButtons.None && BeforeKeys == KeyModifiers.None && After == HeldButtons.None && AfterKeys == KeyModifiers.None;

    /// <summary>True when the press is owned by the stroke button.</summary>
    public bool IsStroke => Anchor == HeldButtons.Stroke;
}
