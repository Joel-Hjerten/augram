using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.UsedBy;

/// <summary>
/// One row of the "Used by…" popup (F3, decision 2026-10-07): the app group › command that is bound to
/// the gesture, with a one-line step summary, and for a Global command its category, the section it sits
/// in on the Commands › Global tab. Also the wording of the delete warning. A projection of what
/// <see cref="MappingStore.UsedBy"/> returns; nothing here decides anything.
/// </summary>
public sealed record UsedByRow(GroupId GroupId, string Group, CommandId CommandId, string Command, string Steps, bool IsActive, string? Category = null)
{
    public const string Separator = " › ";

    /// <summary>"Global › Media › Volume up", "Global › Minimize", "Chrome › Close tab": the F5a vocabulary, as the delete warning lists it.</summary>
    public string Label => Category is null
        ? Group + Separator + Command
        : Group + Separator + Category + Separator + Command;

    /// <summary>The command's name with "(inactive)" when it is switched off, so the popup says why it does not fire.</summary>
    public string CommandText => IsActive ? Command : Command + " (inactive)";

    public static UsedByRow From(AppGroup group, Command command)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(command);
        var steps = command.Steps.Count == 0
            ? "(override to nothing)"
            : string.Join(", ", command.Steps.Select(step => step.Step.Summary));
        var category = group.IsGlobal && command.CategoryId is { } id ? group.FindCategory(id)?.Name : null;
        return new UsedByRow(group.Id, group.Name, command.Id, command.Name, steps, command.IsActive, category);
    }

    /// <summary>The rows for every command bound to <paramref name="gestureId"/>, in document order (Global first, then groups by name).</summary>
    public static IReadOnlyList<UsedByRow> For(MappingStore mapping, GestureId gestureId)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        return mapping.UsedBy(gestureId).Select(pair => From(pair.Group, pair.Command)).ToList();
    }
}
