using Augram.App.Components.StepList;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// What one command row of the <see cref="CommandTree"/> shows (F5a): the glyph of its gesture (or a
/// trigger badge when it has none), name, a one-line step summary ("Minimize window", "3 steps",
/// "Does nothing here" for an override to nothing), the active flag and the F8 platform marker.
/// A projection of a <see cref="Command"/>; the view model resolves the gesture.
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
    public bool HasGlyph => GlyphPoints is { Count: > 0 };

    public bool HasMarker => !string.IsNullOrEmpty(PlatformMarker);

    /// <summary>Projects the command; <paramref name="gesture"/> is the one its trigger names, when it is one and the library still has it.</summary>
    public static CommandItem From(AppGroup group, Command command, Gesture? gesture)
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
            Summarise(group, command),
            StepPlatformMarker.ForCommand(command.Steps));
    }

    private static string Summarise(AppGroup group, Command command) => command.Steps.Count switch
    {
        0 => group.IsGlobal ? "No steps" : "Does nothing here",
        1 => command.Steps[0].Step.Summary,
        var n => $"{n} steps",
    };
}
