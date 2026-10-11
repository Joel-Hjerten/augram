using Augram.Core.Mapping;

namespace Augram.Core.Transfer;

/// <summary>
/// What an export writes (requirements F8: "everything · gestures only · selected app groups (with the gestures they
/// reference)"; plan 0003). A closed set, like <see cref="Trigger"/>: <see cref="Everything"/>, <see cref="GesturesOnly"/>,
/// or a <see cref="Selection"/> of app groups (Global among them when chosen) and ignored apps. <see cref="Exporter"/> reads it.
/// </summary>
public abstract record ExportScope
{
    private ExportScope()
    {
    }

    /// <summary>The options (without the sync and appearance sections), every gesture, the whole mapping.</summary>
    public static ExportScope Everything { get; } = new EverythingScope();

    /// <summary>Every gesture in the library, nothing else.</summary>
    public static ExportScope GesturesOnly { get; } = new GesturesScope();

    /// <summary>
    /// These app groups whole (Global may be one), these ignored apps, and what the groups' commands name: the gestures they use
    /// and the Ignored › Per command entries in their "Not in" (plan 0004), the Exclusions › Global entries in their "Also in"
    /// (plan 0005).
    /// </summary>
    public static Selection Of(IEnumerable<GroupId> groups, IEnumerable<GroupId>? ignored = null)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return new Selection(groups.ToHashSet(), (ignored ?? []).ToHashSet());
    }

    /// <summary><see cref="Everything"/>.</summary>
    public sealed record EverythingScope : ExportScope;

    /// <summary><see cref="GesturesOnly"/>.</summary>
    public sealed record GesturesScope : ExportScope;

    /// <summary>App groups and ignored apps by id; an id the mapping no longer has is passed over.</summary>
    public sealed record Selection(IReadOnlySet<GroupId> Groups, IReadOnlySet<GroupId> Ignored) : ExportScope;
}
