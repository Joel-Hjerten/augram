namespace Augram.Core.Mapping;

/// <summary>
/// When the keys and buttons of a trigger's "while holding" set must have gone down, relative to the press's anchor (SP.net's
/// "Capture Modifiers", learnings 0003 §1–2; its JSON <c>Capture</c> is 0 Before, 1 After, 2 Either). Meaningless, and stored
/// as <see cref="Either"/>, when the set holds nothing besides the anchor.
/// </summary>
public enum HoldCapture
{
    /// <summary>Held before the anchor went down, or pressed while it is held: the set as a whole must equal the trigger's.</summary>
    Either,

    /// <summary>Held when the anchor went down, and nothing pressed after.</summary>
    Before,

    /// <summary>Pressed while the anchor is held, and nothing held before.</summary>
    After,
}
