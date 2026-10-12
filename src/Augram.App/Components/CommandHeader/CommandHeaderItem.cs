using System.Collections.ObjectModel;
using Augram.App.Components.CommandTree;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.App.Components.CommandHeader;

/// <summary>
/// The selected command as the <see cref="CommandHeader"/> shows it: its row (<see cref="CommandItem"/>, every field the tree's
/// row has) and what only the header needs, which a row never carries: the step types "New step…" and "Change to…" grey and why,
/// Options' drag distance and this machine's stroke button (the Drag distance row), and, while a trigger the rules refused waits
/// (Joel, 2026-10-09), the draft over the row's trigger with its note and the Swap and Take it the note offers (Joel,
/// 2026-10-10: what shortcut editors do). The view model builds it from the selected row (<c>CommandsViewModel.HeaderItem</c>).
/// </summary>
public sealed record CommandHeaderItem : CommandItem
{
    /// <summary>The header's item for <paramref name="row"/>, with nothing of the header's own set yet.</summary>
    public CommandHeaderItem(CommandItem row)
        : base(row)
    {
    }

    /// <summary>
    /// The step types the command cannot take a step of now, each with the reason "New step…" greys it with ("A Remap step is a
    /// command's only step."); a type left out can be added. Core's answer (<see cref="StepOffer"/>, the draft's trigger while
    /// one waits).
    /// </summary>
    public IReadOnlyDictionary<IStepType, string> StepRefusals { get; init; } = ReadOnlyDictionary<IStepType, string>.Empty;

    /// <summary>
    /// By index of the command's steps here, the step types that step cannot be changed to now (the step menu's "Change to…"), each
    /// with the reason the picker greys it with ("This step is Hotkey already.", "A Remap step is a command's only step."). Core's
    /// answer, judged as a replacement (<see cref="StepOffer.ChangeRefusals"/>, the draft's trigger while one waits).
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<IStepType, string>> ChangeRefusals { get; init; } = [];

    /// <summary>The "Steps for" row (plan 0010): the side of the steps in view, its label, note and buttons; null hides the row.</summary>
    public StepSide? StepSide { get; init; }

    /// <summary>
    /// Options › Strokes › Capture's button drag distance (plan 0004), what a trigger without its own falls back to: the header's
    /// "Options value (10 px)".
    /// </summary>
    public int OptionsDragDistancePx { get; init; } = CaptureThresholds.Default.ButtonDragDistancePx;

    /// <summary>This machine's stroke button; null in tests and the gallery.</summary>
    public MouseButton? StrokeButton { get; init; }

    /// <summary>
    /// The header shows "Drag distance" (plan 0004): a bound trigger (or its draft) whose set holds buttons other than the
    /// stroke button and none of it, so its presses are held back and handed back as drags (<see cref="TriggerHold.HandsBackDrags"/>).
    /// Not where the button it holds is this machine's stroke button: there it draws, and the distance does nothing.
    /// </summary>
    public bool ShowsDragDistance => HoldRemap is null && Trigger.IsBound && Trigger.Hold.HandsBackDrags
        && (StrokeButton is not { } stroke || Trigger.Hold.ForStrokeButton(stroke).HandsBackDrags);

    /// <summary>
    /// The header's note on a trigger the rules refused, kept as a draft while the user composes it (Joel, 2026-10-09): what is
    /// wrong and how to fix it. Null for the stored trigger.
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
    public CommandHeaderItem WithDraft(Trigger draft, Gesture? gesture, AppGroup group, HostPlatform here, string note)
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
    public CommandHeaderItem WithConflict(string conflictName, bool canSwap, bool canTake)
    {
        ArgumentNullException.ThrowIfNull(conflictName);
        return this with { ConflictName = conflictName, CanSwapTrigger = canSwap, CanTakeTrigger = canTake };
    }
}
