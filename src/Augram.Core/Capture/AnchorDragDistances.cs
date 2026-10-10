namespace Augram.Core.Capture;

/// <summary>
/// How far a press of each anchor may move over one window before it is handed back to the app as a drag (Joel, 2026-10-10,
/// plan 0004): the commands that hold the button there may each set their own distance, and the press is decided before the
/// wheel says which command it is, so the largest wins and a command without its own counts as Options › Capture's
/// (<see cref="CaptureThresholds.ButtonDragDistancePx"/>). Worked out with the <see cref="AnchorPlan"/> off the hook thread by
/// <c>Mapping.AnchorPlanner</c>; the hook hands it to the machine with the press, untouched. One <see cref="long"/>: per button
/// (bit offset 9 × <see cref="MouseButton"/> value) 8 bits of the largest own distance (0: none) and 1 bit "a command uses the
/// Options value". <see cref="None"/> (no command, or a press decided before the planner said) uses the Options value.
/// </summary>
public readonly record struct AnchorDragDistances(long Bits)
{
    private const int SlotBits = 9;
    private const int UsesDefaultBit = 8;
    private const long DistanceMask = 0xFF;

    public static AnchorDragDistances None => default;

    /// <summary>These distances with a command holding <paramref name="anchor"/> at its own <paramref name="distancePx"/> (1 to <see cref="CaptureThresholds.MaxButtonDragDistancePx"/>).</summary>
    public AnchorDragDistances WithOwn(MouseButton anchor, int distancePx)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(distancePx, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(distancePx, CaptureThresholds.MaxButtonDragDistancePx);
        var shift = Shift(anchor);
        var own = (Bits >> shift) & DistanceMask;
        return distancePx <= own ? this : new((Bits & ~(DistanceMask << shift)) | ((long)distancePx << shift));
    }

    /// <summary>These distances with a command holding <paramref name="anchor"/> at the Options value.</summary>
    public AnchorDragDistances WithOptionsValue(MouseButton anchor) => new(Bits | (1L << (Shift(anchor) + UsesDefaultBit)));

    /// <summary>The distance for a press <paramref name="anchor"/> owns, the Options value being <paramref name="optionsPx"/>.</summary>
    public int For(MouseButton anchor, int optionsPx)
    {
        var shift = Shift(anchor);
        var own = (int)((Bits >> shift) & DistanceMask);
        var usesOptions = (Bits & (1L << (shift + UsesDefaultBit))) != 0;
        return own == 0 ? optionsPx : usesOptions ? Math.Max(own, optionsPx) : own;
    }

    private static int Shift(MouseButton anchor) => (int)anchor * SlotBits;
}
