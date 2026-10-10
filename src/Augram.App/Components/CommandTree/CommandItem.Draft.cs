using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// The trigger draft half of <see cref="CommandItem"/> (Joel, 2026-10-09): what the header shows while a trigger (or an input)
/// the rules refused waits, and, when another command of the group uses it here (Joel, 2026-10-10: what shortcut editors do),
/// the Swap and Take it the header offers in the draft's note. Only the header's item carries any of it; a row never does.
/// </summary>
public sealed partial record CommandItem
{
    /// <summary>
    /// The header's note on a trigger the rules refused, kept as a draft while the user composes it (Joel, 2026-10-09): what is
    /// wrong and how to fix it. Null for the stored trigger, and always on a row (<see cref="WithDraft"/>).
    /// </summary>
    public string? DraftNote { get; init; }

    /// <summary>
    /// The command of the group that already uses the draft here and is all that refuses it (Joel, 2026-10-10), named on the
    /// note's Swap and Take it; null without a draft, or for any other refusal (a missing button, a clash only on the other
    /// platform), which offers neither.
    /// </summary>
    public string? ConflictName { get; init; }

    /// <summary>
    /// Swap: this command takes the draft and <see cref="ConflictName"/> takes this command's stored trigger (or input) here.
    /// Set only when the rules accept that pair and there is a stored one to give.
    /// </summary>
    public bool CanSwapTrigger { get; init; }

    /// <summary>Take it: this command takes the draft and <see cref="ConflictName"/> is left with none here. Set only when the rules accept that.</summary>
    public bool CanTakeTrigger { get; init; }

    /// <summary>
    /// The item as the header shows it while <paramref name="draft"/> waits (a trigger the rules refused): its kind, words, glyph
    /// (<paramref name="gesture"/>, for a gesture), hint and anchor warning, with <paramref name="note"/>. The row keeps the stored item.
    /// </summary>
    public CommandItem WithDraft(Trigger draft, Gesture? gesture, AppGroup group, HostPlatform here, string note)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(group);
        return this with
        {
            TriggerKind = TriggerKindExtensions.KindOf(draft),
            TriggerText = HoldRemap is null ? TriggerKindExtensions.Text(draft, gesture?.Name ?? "Missing gesture", here) : InputKindExtensions.Text(draft),
            GlyphPoints = gesture is { Samples.Count: > 0 } ? gesture.Samples[0] : null,
            Trigger = draft,
            TriggerHint = HoldRemap is { } holdRemap ? InputKindExtensions.Hint(holdRemap) : TriggerKindExtensions.Hint(draft, here),
            AnchorWarning = TriggerKindExtensions.AnchorWarning(draft, group),
            DraftNote = note,
        };
    }

    /// <summary>The draft's item with what its note offers against <paramref name="conflictName"/>: Swap, Take it, either or neither.</summary>
    public CommandItem WithConflict(string conflictName, bool canSwap, bool canTake)
    {
        ArgumentNullException.ThrowIfNull(conflictName);
        return this with { ConflictName = conflictName, CanSwapTrigger = canSwap, CanTakeTrigger = canTake };
    }
}
