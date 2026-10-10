using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// What one command row of the <see cref="CommandTree"/> shows (F5a): the glyph of its gesture (or a
/// trigger badge when it has none), name, a one-line step summary ("Minimize window", "3 steps",
/// "Does nothing here" for an override to nothing), the active flag, the F8 platform marker and, in an
/// app group that has categories, the category as a small tag. It also carries what the header's
/// Category dropdown offers for it, and for a command under a hold remap (F9, plan 0002) that hold remap, so the header shows
/// the Input editor instead of the trigger. A projection of a <see cref="Command"/>; the view model resolves
/// the gesture and decides the section, the tag and the choices.
/// </summary>
public sealed record CommandItem(
    CommandId Id,
    GroupId GroupId,
    string Name,
    bool IsActive,
    TriggerKind TriggerKind,
    string TriggerText,
    IReadOnlyList<GesturePoint>? GlyphPoints,
    string StepSummary,
    string? PlatformMarker)
{
    /// <summary>The header's Not in row for a Global command used in every app group.</summary>
    public const string NoneNotIn = "none";

    /// <summary>The section the row sits in: its group on the Apps tab, its category (or Uncategorized) on the Global tab.</summary>
    public SectionId Section { get; init; }

    /// <summary>The command's category in its group; null is Uncategorized.</summary>
    public CategoryId? CategoryId { get; init; }

    /// <summary>
    /// The hold remap the command sits under (F9, plan 0002); null for an ordinary command. Its trigger is then an input (or
    /// none yet), which the header edits in place of the trigger kind and the "While holding" boxes.
    /// </summary>
    public HoldRemap? HoldRemap { get; init; }

    /// <summary>A command under a hold remap: the header shows its Input, the step picker offers the Remap step.</summary>
    public bool IsUnderHoldRemap => HoldRemap is not null;

    /// <summary>The small tag on the row (an app group with categories, e.g. Photoshop); null shows none.</summary>
    public string? CategoryLabel { get; init; }

    /// <summary>What the header's Category dropdown offers, Uncategorized first; empty hides the dropdown.</summary>
    public IReadOnlyList<CategoryChoice> Categories { get; init; } = [];

    /// <summary>Where the command itself takes part (F8 "Use on"; its own stored value), for the header's check boxes.</summary>
    public PlatformSet UseOn { get; init; } = PlatformSet.All;

    /// <summary>
    /// The platforms its app group and its category leave it (<see cref="AppGroup.UseOnLimitFor"/>, Joel 2026-10-08): the
    /// header shows a platform outside it unchecked and disabled, whatever <see cref="UseOn"/> says.
    /// </summary>
    public PlatformSet UseOnLimit { get; init; } = PlatformSet.All;

    /// <summary>Who sets <see cref="UseOnLimit"/>: "Set by category 'Personal': Windows only"; null when nothing limits the command.</summary>
    public string? UseOnLimitText { get; init; }

    /// <summary>F8: not used on the platform Augram runs on (its group, category or itself leave it out); listed greyed only while the list shows other platforms.</summary>
    public bool IsElsewhere { get; init; }

    /// <summary>F8: the header's line about this platform's steps (original, converted, own version, changed since); null for none.</summary>
    public string? VersionText { get; init; }

    /// <summary>F8: this platform runs its own steps, which "Use the converted original" drops.</summary>
    public bool HasOwnVersionHere { get; init; }

    /// <summary>F8: the own version was made before the original last changed.</summary>
    public bool IsOwnVersionStale { get; init; }

    /// <summary>The trigger as it reads on this platform (its own, the original, or the original converted); what the header's boxes show.</summary>
    public Trigger Trigger { get; init; } = Trigger.None;

    /// <summary>How the trigger fires, for a wheel or a click trigger; null otherwise.</summary>
    public string? TriggerHint { get; init; }

    /// <summary>The anchors other than the stroke button and where their clicks wait (Joel, 2026-10-09); null when there are none.</summary>
    public string? AnchorWarning { get; init; }

    /// <summary>F8 for the trigger: converted from the other platform, this platform's own, or no counterpart here; null for a trigger authored here.</summary>
    public string? TriggerNote { get; init; }

    /// <summary>
    /// The header's note on a trigger the rules refused, kept as a draft while the user composes it (Joel, 2026-10-09): what is
    /// wrong and how to fix it. Null for the stored trigger, and always on a row (<see cref="WithDraft"/>).
    /// </summary>
    public string? DraftNote { get; init; }

    /// <summary>
    /// Options › Capture's button drag distance (plan 0004), what a trigger without its own falls back to: the header's "Options
    /// value (10 px)". The view model sets it on the header's item; rows keep the default.
    /// </summary>
    public int OptionsDragDistancePx { get; init; } = CaptureThresholds.Default.ButtonDragDistancePx;

    /// <summary>
    /// The header shows "Drag distance" (plan 0004): a bound trigger (or its draft) whose set holds buttons other than the
    /// stroke button and none of it, so its presses are held back and handed back as drags (<see cref="TriggerHold.HandsBackDrags"/>).
    /// </summary>
    public bool ShowsDragDistance => HoldRemap is null && Trigger.IsBound && Trigger.Hold.HandsBackDrags;

    /// <summary>The header shows "Not in" (plan 0004): a Global command; never an app group's, never one under a hold remap.</summary>
    public bool CanSetNotIn { get; init; }

    /// <summary>The app groups a Global command is not used in (<see cref="Command.NotIn"/>), as stored.</summary>
    public IReadOnlyList<GroupId> NotIn { get; init; } = [];

    /// <summary>What the header's "Not in" row says: those app groups by name ("Eyeris, Spine"), or <see cref="NoneNotIn"/>.</summary>
    public string NotInText { get; init; } = NoneNotIn;

    public bool HasGlyph => GlyphPoints is { Count: > 0 };

    public bool HasMarker => !string.IsNullOrEmpty(PlatformMarker);

    public bool HasCategoryLabel => !string.IsNullOrEmpty(CategoryLabel);

    /// <summary>
    /// Projects the command into its group's section, with no tag and no category choices;
    /// <paramref name="gesture"/> is the one its trigger on <paramref name="here"/> names (<see cref="Command.TriggerFor"/>),
    /// when it is one and the library still has it; the trigger, the step summary and the marker read as on <paramref name="here"/>.
    /// A trigger holding more than the stroke button leads the summary ("Shift + Undo · Minimize window").
    /// </summary>
    public static CommandItem From(AppGroup group, Command command, Gesture? gesture, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(command);
        var trigger = command.TriggerFor(here);
        var holdRemap = group.HoldRemapOf(command);
        var kind = TriggerKindExtensions.KindOf(trigger);
        var triggerText = holdRemap is null ? TriggerKindExtensions.Text(trigger, gesture?.Name ?? "Missing gesture", here) : InputKindExtensions.Text(trigger);
        var points = gesture is { Samples.Count: > 0 } ? gesture.Samples[0] : null;
        var usedHere = group.IsCommandUsedOn(command, here);
        var steps = Summarise(group, command, here);
        return new CommandItem(
            command.Id,
            group.Id,
            command.Name,
            command.IsActive,
            kind,
            triggerText,
            points,
            trigger.IsBound && !trigger.Hold.IsDefault ? $"{triggerText} · {steps}" : steps,
            usedHere ? StepPlatformMarker.ForCommand(command, here) : StepPlatformMarker.Only(group.EffectiveUseOn(command)))
        {
            Trigger = trigger,
            TriggerHint = holdRemap is null ? TriggerKindExtensions.Hint(trigger, here) : InputKindExtensions.Hint(holdRemap),
            AnchorWarning = TriggerKindExtensions.AnchorWarning(trigger, group),
            TriggerNote = TriggerLine(command, here),
            Section = holdRemap is null ? SectionId.ForGroup(group.Id) : SectionId.ForHoldRemap(group.Id, holdRemap.Id),
            CategoryId = command.CategoryId,
            HoldRemap = holdRemap,
            UseOn = command.UseOn,
            UseOnLimit = group.UseOnLimitFor(command),
            UseOnLimitText = LimitLine(group, command),
            IsElsewhere = !usedHere,
            VersionText = VersionLine(command, here),
            HasOwnVersionHere = command.OwnVersion?.Platform == here,
            IsOwnVersionStale = command.IsOwnVersionStale,
            CanSetNotIn = group.IsGlobal && holdRemap is null,
            NotIn = command.NotIn,
        };
    }

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

    private static HostPlatform Other(HostPlatform platform) => platform == HostPlatform.MacOS ? HostPlatform.Windows : HostPlatform.MacOS;

    /// <summary>"Set by category 'Personal': Windows only", "Set by app group 'Steam' and hold remap 'Space': …"; null when nothing limits the command.</summary>
    private static string? LimitLine(AppGroup group, Command command)
    {
        var limit = group.UseOnLimitFor(command);
        if (limit == PlatformSet.All)
        {
            return null;
        }

        var by = new List<string>(2);
        if (!group.IsGlobal && group.UseOn != PlatformSet.All)
        {
            by.Add($"app group '{group.Name}'");
        }

        if (group.CategoryOf(command) is { } category && category.UseOn != PlatformSet.All)
        {
            by.Add($"category '{category.Name}'");
        }

        if (group.HoldRemapOf(command) is { } holdRemap && holdRemap.UseOn != PlatformSet.All)
        {
            by.Add($"hold remap '{holdRemap.Name}'");
        }

        return $"Set by {string.Join(" and ", by)}: {StepPlatformMarker.Only(limit)}";
    }

    /// <summary>
    /// The header's line about this platform's steps (F8): where they come from and what an edit here does; null for a
    /// command authored here without an own version elsewhere, or with no steps yet.
    /// </summary>
    private static string? VersionLine(Command command, HostPlatform here)
    {
        var name = StepPlatformMarker.Name(here);
        if (command.OwnVersion is { } own && own.Platform == here)
        {
            var origin = StepPlatformMarker.Name(Other(here));
            return command.IsOwnVersionStale
                ? $"Own {name} steps. The {origin} original changed since they were made: check them, then mark them as checked."
                : $"Own {name} steps; the {origin} original runs on {origin}.";
        }

        if (command.OwnVersion is { } other)
        {
            var otherName = StepPlatformMarker.Name(other.Platform);
            return command.IsOwnVersionStale
                ? $"{name} original. {otherName} has its own steps, made before this original last changed."
                : $"{name} original. {otherName} has its own steps.";
        }

        return command.Origin is { } authored && authored != here
            ? $"{StepPlatformMarker.Name(authored)} original, converted for {name}. Editing a step here makes own {name} steps; {StepPlatformMarker.Name(authored)} keeps the original."
            : null;
    }

    /// <summary>
    /// The header's line about this platform's trigger (F8, Joel 2026-10-09: keys convert like hotkeys, an edit here makes it
    /// this platform's own); null for a trigger authored here, or one whose keys need no conversion.
    /// </summary>
    private static string? TriggerLine(Command command, HostPlatform here)
    {
        var name = StepPlatformMarker.Name(here);
        if (command.HasOwnTriggerOn(here))
        {
            return $"Own {name} trigger; the original keeps its own on {StepPlatformMarker.Name(Other(here))}.";
        }

        if (command.Origin is not { } origin || origin == here)
        {
            return null;
        }

        var plan = command.TriggerPlanFor(here);
        if (plan.Trigger is null)
        {
            return $"{plan.Reason}. A trigger chosen here is {name}'s own.";
        }

        return plan.IsConverted
            ? $"{StepPlatformMarker.Name(origin)} trigger {command.Trigger.Describe(origin)}, converted for {name}: {plan.Trigger.Describe(here)}. Changing it here makes it {name}'s own."
            : null;
    }

    private static string Summarise(AppGroup group, Command command, HostPlatform here) => command.StepsFor(here).Count switch
    {
        0 => group.IsGlobal ? "No steps" : "Does nothing here",
        1 => StepPlatformMarker.For(command.StepsFor(here)[0], here).Summary,
        var n => $"{n} steps",
    };
}
