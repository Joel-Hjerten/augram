namespace Augram.Core.Capture;

/// <summary>
/// Which presses the hook holds back over one window, and which further buttons it takes into a press (F1 "Triggers as
/// combinations"; learnings 0003 §4; Joel 2026-10-09: per app). An <em>anchor</em> is a button whose down is held back
/// because an active command that applies there holds it without the stroke button ("Right + wheel up"); the stroke button
/// is always one and is not listed. For each anchor, and for the stroke button, the <em>extras</em> are the other buttons
/// some command holds together with it: pressed while that press is held, their down and up are swallowed and they become
/// part of the press. One <see cref="long"/> so the hook reads it as one volatile (CLAUDE.md invariant 1); worked out off
/// the hook thread by <c>Mapping.AnchorPlanner</c> for the window under the pointer.
/// <para>Bits: 0–4 the anchors (bit = <see cref="MouseButton"/> value); then six 5-bit extras masks, one per anchor slot
/// (slots 0–4 a physical anchor, slot 5 the stroke button).</para>
/// </summary>
public readonly record struct AnchorPlan(long Bits)
{
    private const int ButtonCount = 5;
    private const int StrokeSlot = 5;
    private const long Mask = (1L << ButtonCount) - 1;

    /// <summary>No anchors besides the stroke button, no extras: every other button is untouched.</summary>
    public static AnchorPlan None => default;

    public bool IsEmpty => Bits == 0;

    /// <summary>True when a press of <paramref name="button"/> is held back here although it is not the stroke button.</summary>
    public bool IsAnchor(MouseButton button) => (Bits & (1L << (int)button)) != 0;

    /// <summary>The buttons taken into a press owned by <paramref name="owner"/> (the stroke button when <paramref name="ownerIsStroke"/>).</summary>
    public HeldButtons ExtrasFor(MouseButton owner, bool ownerIsStroke)
    {
        var bits = (Bits >> Shift(ownerIsStroke ? StrokeSlot : (int)owner)) & Mask;
        return (HeldButtons)(bits << 1);
    }

    /// <summary>True when <paramref name="other"/>, pressed during a press owned by <paramref name="owner"/>, joins it.</summary>
    public bool Claims(MouseButton owner, bool ownerIsStroke, MouseButton other) => ExtrasFor(owner, ownerIsStroke).Has(other);

    /// <summary>This plan with <paramref name="button"/> held back as an anchor.</summary>
    public AnchorPlan WithAnchor(MouseButton button) => new(Bits | (1L << (int)button));

    /// <summary>This plan with <paramref name="extras"/> (physical flags) taken into presses owned by <paramref name="owner"/>.</summary>
    public AnchorPlan WithExtras(MouseButton owner, bool ownerIsStroke, HeldButtons extras)
    {
        var bits = ((long)(extras & HeldButtonsExtensions.Physical) >> 1) & Mask;
        return new(Bits | (bits << Shift(ownerIsStroke ? StrokeSlot : (int)owner)));
    }

    private static int Shift(int slot) => ButtonCount + (slot * ButtonCount);
}
