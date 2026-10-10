namespace Augram.Core.Transfer;

/// <summary>How a file item whose id is unknown here was matched to a local item (plan 0003, decision 5).</summary>
public enum ImportMatchKind
{
    /// <summary>The same name in the same place (case-insensitive, as the rules compare names).</summary>
    Name,

    /// <summary>A hold remap on the same hold key in the matched group.</summary>
    HoldKey,

    /// <summary>A gesture whose first sample scores as a duplicate of one here (A7).</summary>
    Shape,
}
