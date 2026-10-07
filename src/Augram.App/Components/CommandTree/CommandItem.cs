using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// What one command row of the <see cref="CommandTree"/> shows (F5a): the glyph of its gesture (or a
/// trigger badge when it has none), name, a one-line step summary ("Minimize window", "3 steps",
/// "Does nothing here" for an override to nothing), the active flag, the F8 platform marker and, in an
/// app group that has categories, the category as a small tag. It also carries what the header's
/// Category dropdown offers for it. A projection of a <see cref="Command"/>; the view model resolves
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
    /// <summary>The section the row sits in: its group on the Apps tab, its category (or Uncategorized) on the Global tab.</summary>
    public SectionId Section { get; init; }

    /// <summary>The command's category in its group; null is Uncategorized.</summary>
    public CategoryId? CategoryId { get; init; }

    /// <summary>The small tag on the row (an app group with categories, e.g. Photoshop); null shows none.</summary>
    public string? CategoryLabel { get; init; }

    /// <summary>What the header's Category dropdown offers, Uncategorized first; empty hides the dropdown.</summary>
    public IReadOnlyList<CategoryChoice> Categories { get; init; } = [];

    /// <summary>Where the command takes part (F8 "Use on"), for the header's check boxes.</summary>
    public PlatformSet UseOn { get; init; } = PlatformSet.All;

    /// <summary>F8: not used on the platform Augram runs on; listed greyed only while the list shows other platforms.</summary>
    public bool IsElsewhere { get; init; }

    /// <summary>F8: the header's line about this platform's steps (original, converted, own version, changed since); null for none.</summary>
    public string? VersionText { get; init; }

    /// <summary>F8: this platform runs its own steps, which "Use the converted original" drops.</summary>
    public bool HasOwnVersionHere { get; init; }

    /// <summary>F8: the own version was made before the original last changed.</summary>
    public bool IsOwnVersionStale { get; init; }

    public bool HasGlyph => GlyphPoints is { Count: > 0 };

    public bool HasMarker => !string.IsNullOrEmpty(PlatformMarker);

    public bool HasCategoryLabel => !string.IsNullOrEmpty(CategoryLabel);

    /// <summary>
    /// Projects the command into its group's section, with no tag and no category choices;
    /// <paramref name="gesture"/> is the one its trigger names, when it is one and the library still has it; the step summary
    /// and the marker read as on <paramref name="here"/>.
    /// </summary>
    public static CommandItem From(AppGroup group, Command command, Gesture? gesture, HostPlatform here)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(command);
        var kind = TriggerKindExtensions.KindOf(command.Trigger);
        var triggerText = kind == TriggerKind.Gesture ? gesture?.Name ?? "Missing gesture" : kind.Label();
        var points = gesture is { Samples.Count: > 0 } ? gesture.Samples[0] : null;
        return new CommandItem(
            command.Id,
            group.Id,
            command.Name,
            command.IsActive,
            kind,
            triggerText,
            points,
            Summarise(group, command, here),
            command.IsUsedOn(here) ? StepPlatformMarker.ForCommand(command, here) : $"{StepPlatformMarker.Name(Other(here))} only")
        {
            Section = SectionId.ForGroup(group.Id),
            CategoryId = command.CategoryId,
            UseOn = command.UseOn,
            IsElsewhere = !command.IsUsedOn(here),
            VersionText = VersionLine(command, here),
            HasOwnVersionHere = command.OwnVersion?.Platform == here,
            IsOwnVersionStale = command.IsOwnVersionStale,
        };
    }

    private static HostPlatform Other(HostPlatform platform) => platform == HostPlatform.MacOS ? HostPlatform.Windows : HostPlatform.MacOS;

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

    private static string Summarise(AppGroup group, Command command, HostPlatform here) => command.StepsFor(here).Count switch
    {
        0 => group.IsGlobal ? "No steps" : "Does nothing here",
        1 => StepPlatformMarker.For(command.StepsFor(here)[0], here).Summary,
        var n => $"{n} steps",
    };
}
