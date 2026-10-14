using Augram.App.Components.StepList;
using Augram.Core.Gestures;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// What one command row of the <see cref="CommandTree"/> shows (F5a): the glyph of its gesture (or a
/// trigger badge when it has none), name, a one-line step summary ("Minimize window", "3 steps",
/// "Does nothing here" for an override to nothing), the active flag, the F8 platform badge and marker and, in an
/// app group that has categories, the category as a small tag. It also carries what the header's
/// Category dropdown offers for it, and for a command under a hold remap (F9, plan 0002) that hold remap, so the header shows
/// the Input editor instead of the trigger. A projection of a <see cref="Command"/>, built by the view model
/// (<c>ViewModels/Commands/Projection/CommandItemProjection</c>), which resolves the gesture and decides the section, the tag and
/// the choices. What only the header needs (the step refusals, the trigger draft, the Options drag distance, this machine's
/// stroke button) is on <c>CommandHeaderItem</c>, the header's item built from the row; a row never carries it.
/// </summary>
public record CommandItem(
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
    /// <summary>The header's Not in (or Also in) row for a command that names no Exclusions › Per command (or Global) entry.</summary>
    public const string NoneNotIn = "none";

    /// <summary>The section the row sits in: its group on the Apps tab, its category on the Global tab.</summary>
    public SectionId Section { get; init; }

    /// <summary>The command's category in its group; null is none (an app group's command only).</summary>
    public CategoryId? CategoryId { get; init; }

    /// <summary>
    /// The hold remap the command sits under (F9, plan 0002); null for an ordinary command. Its trigger is then an input (or
    /// none yet), which the header edits in place of the trigger kind and the "While holding" boxes.
    /// </summary>
    public HoldRemap? HoldRemap { get; init; }

    /// <summary>A command under a hold remap: the header shows its Input in place of the trigger.</summary>
    public bool IsUnderHoldRemap => HoldRemap is not null;

    /// <summary>The small tag on the row (an app group with categories, e.g. Photoshop); null shows none.</summary>
    public string? CategoryLabel { get; init; }

    /// <summary>What the header's Category dropdown offers (an app group's led by "No category"); empty hides the dropdown.</summary>
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

    /// <summary>The row's platform badge ("Win", "Win + Mac"); <see cref="PlatformMarker"/> is its tooltip and the Steps for row's note. Null shows none.</summary>
    public PlatformBadge? PlatformBadge { get; init; }

    /// <summary>F8: not used on the platform Augram runs on (its group, category or itself leave it out); listed greyed only while the list shows other platforms.</summary>
    public bool IsElsewhere { get; init; }

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

    /// <summary>The header shows "Not in" (plan 0004): any command (Global's or an app group's) not under a hold remap.</summary>
    public bool CanSetNotIn { get; init; }

    /// <summary>The Exclusions › Per command entries the command is not used over (<see cref="Command.NotIn"/>), as stored.</summary>
    public IReadOnlyList<GroupId> NotIn { get; init; } = [];

    /// <summary>What the header's "Not in" row says: those entries by name ("Eyeris, Spine"), or <see cref="NoneNotIn"/>.</summary>
    public string NotInText { get; init; } = NoneNotIn;

    /// <summary>
    /// The header shows "Also in" (plan 0005 decision 7): a command not under a hold remap whose stored trigger here holds a button
    /// other than the stroke button and not the stroke button (<see cref="TriggerHold.HandsBackDrags"/>: a button trigger, Right +
    /// wheel), the only kind that may still work over an Exclusions › Global entry. The header's item also leaves it out where the
    /// button held is this machine's stroke button. Follows the stored trigger, not a draft: Core keeps the list only on such a one.
    /// </summary>
    public bool CanSetAlsoIn { get; init; }

    /// <summary>The Exclusions › Global entries the command still works over (<see cref="Command.AlsoIn"/>), as stored.</summary>
    public IReadOnlyList<GroupId> AlsoIn { get; init; } = [];

    /// <summary>What the header's "Also in" row says: those entries by name ("Blender"), or <see cref="NoneNotIn"/> ("none").</summary>
    public string AlsoInText { get; init; } = NoneNotIn;

    public bool HasGlyph => GlyphPoints is { Count: > 0 };

    public bool HasCategoryLabel => !string.IsNullOrEmpty(CategoryLabel);
}
