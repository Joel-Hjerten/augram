namespace Augram.Core.Capture;

/// <summary>Conversions between <see cref="MouseButton"/> and <see cref="HeldButtons"/>, and the display order of buttons.</summary>
public static class HeldButtonsExtensions
{
    /// <summary>Every physical button flag (all but <see cref="HeldButtons.Stroke"/>).</summary>
    public const HeldButtons Physical = HeldButtons.Left | HeldButtons.Middle | HeldButtons.Right | HeldButtons.X1 | HeldButtons.X2;

    /// <summary>Every flag a stored set may hold.</summary>
    public const HeldButtons All = HeldButtons.Stroke | Physical;

    /// <summary>The physical buttons in the order the trigger editor and the summaries list them (Joel, 2026-10-09).</summary>
    public static IReadOnlyList<MouseButton> DisplayOrder { get; } = [MouseButton.Left, MouseButton.Right, MouseButton.Middle, MouseButton.X1, MouseButton.X2];

    /// <summary>The flag of one physical button.</summary>
    public static HeldButtons Flag(this MouseButton button) => (HeldButtons)(2 << (int)button);

    public static bool Has(this HeldButtons set, MouseButton button) => (set & button.Flag()) != 0;

    /// <summary>The physical buttons in the set, in <see cref="DisplayOrder"/>.</summary>
    public static IEnumerable<MouseButton> Buttons(this HeldButtons set) => DisplayOrder.Where(button => set.Has(button));

    /// <summary>
    /// The set as it reads on a machine whose stroke button is <paramref name="strokeButton"/>: that button named explicitly is
    /// the stroke button there, so "Middle" in a trigger made where Right is the stroke button means the stroke button where
    /// Middle is.
    /// </summary>
    public static HeldButtons ForStrokeButton(this HeldButtons set, MouseButton strokeButton)
        => set.Has(strokeButton) ? (set & ~strokeButton.Flag()) | HeldButtons.Stroke : set;
}
