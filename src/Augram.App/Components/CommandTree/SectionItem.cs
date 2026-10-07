using System.Globalization;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// One collapsible section of the <see cref="CommandTree"/> (F5a; Global/Apps split, Joel 2026-10-07): an
/// app group on the Apps tab, a category or Uncategorized on the Global tab. The item says what its header
/// offers, so the tree never asks which tab it is on: <see cref="CanRename"/>, <see cref="CanDelete"/>,
/// <see cref="CanEditDefinition"/> (the app group form) and <see cref="CanToggleActive"/>. It carries all
/// its commands, sorted by the store; the tree hides them while the section is collapsed.
/// </summary>
public sealed record SectionItem(SectionId Id, string Name, bool IsActive, bool IsExpanded, IReadOnlyList<CommandItem> Commands)
{
    public bool CanRename { get; init; }

    public bool CanDelete { get; init; }

    /// <summary>The app group form in the side panel: app groups only.</summary>
    public bool CanEditDefinition { get; init; }

    /// <summary>The header's active check box: app groups only (a category has no active flag).</summary>
    public bool CanToggleActive { get; init; }

    /// <summary>F8: a group used only on the other platform ("Windows only"), shown greyed when the list shows other platforms.</summary>
    public bool IsElsewhere { get; init; }

    /// <summary>A short F8 note after the count: "Windows only", "no macOS name"; null for none.</summary>
    public string? Note { get; init; }

    /// <summary>"3 commands", "1 command", "no commands", then the <see cref="Note"/> when there is one: "19 commands · Windows only".</summary>
    public string CountText => Note is null ? Count : $"{Count} · {Note}";

    private string Count => Commands.Count switch
    {
        0 => "no commands",
        1 => "1 command",
        var n => n.ToString(CultureInfo.InvariantCulture) + " commands",
    };
}
