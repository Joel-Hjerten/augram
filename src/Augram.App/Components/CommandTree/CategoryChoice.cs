using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// One entry of the command header's Category dropdown: a category of the command's group, or, in an app group,
/// <see cref="None"/> (<see cref="Id"/> null). Global has no such entry: every Global command is in a category, Uncategorized
/// being an ordinary one (Joel, 2026-10-11).
/// </summary>
public sealed record CategoryChoice(CategoryId? Id, string Name)
{
    public const string NoneName = "No category";

    /// <summary>An app group's command without a category; its row shows no tag.</summary>
    public static CategoryChoice None { get; } = new(null, NoneName);
}
