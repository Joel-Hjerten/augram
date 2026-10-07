using Augram.Core.Gestures;

namespace Augram.Core.Mapping;

/// <summary>
/// A named command in an app group (F5a): one trigger, the steps played in order when it fires.
/// An empty step list in an app group is the "override to nothing" in daily use in the reference
/// config (F5: Steam games ignore the global Close). Inactive commands stay in the list and are
/// invisible to the resolver. <paramref name="Note"/> is free text shown read-only; the importer keeps
/// a script-only SP.net action's script there. <paramref name="CategoryId"/> is the section of its group
/// it is sorted into (<see cref="AppGroup.Categories"/>); null is "Uncategorized". Immutable: a change is a new record committed through
/// <see cref="MappingStore"/>.
/// </summary>
public sealed record Command(
    CommandId Id,
    string Name,
    Trigger Trigger,
    bool IsActive,
    IReadOnlyList<CommandStep> Steps,
    string? Note = null,
    CategoryId? CategoryId = null)
{
    /// <summary>No steps: in an app group this shadows the global command for the same trigger with nothing.</summary>
    public bool IsOverrideToNothing => Steps.Count == 0;

    public bool UsesGesture(GestureId gestureId)
        => Trigger is Trigger.GestureTrigger gesture && gesture.GestureId == gestureId;
}
