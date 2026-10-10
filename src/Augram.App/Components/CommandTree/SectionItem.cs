using System.Globalization;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// One collapsible section of the <see cref="CommandTree"/> (F5a; Global/Apps split, Joel 2026-10-07): an
/// app group on the Apps tab, a category or Uncategorized on the Global tab, or a hold remap (F9, plan 0002) nested in its
/// app group's section (<see cref="IsNested"/>, listed right after it). The item says what its header offers, so the tree
/// never asks which tab it is on: <see cref="CanRename"/>, <see cref="CanDelete"/>, <see cref="CanEditDefinition"/> (the app
/// group form), <see cref="CanToggleActive"/>, <see cref="CanCopy"/> and <see cref="CanAddHoldRemap"/>. It carries all its
/// commands, sorted by the store; the tree hides them while the section (or the section it is nested in) is collapsed.
/// </summary>
public sealed record SectionItem(SectionId Id, string Name, bool IsActive, bool IsExpanded, IReadOnlyList<CommandItem> Commands)
{
    public bool CanRename { get; init; }

    public bool CanDelete { get; init; }

    /// <summary>The app group form in the side panel: app groups only.</summary>
    public bool CanEditDefinition { get; init; }

    /// <summary>The header's active check box: app groups and hold remaps (a category has no active flag).</summary>
    public bool CanToggleActive { get; init; }

    /// <summary>Copy acts on the section itself: a hold remap, with its commands.</summary>
    public bool CanCopy { get; init; }

    /// <summary>The menu offers New hold remap here: an app group on the Apps tab (never Global, plan 0002 decision 1).</summary>
    public bool CanAddHoldRemap { get; init; }

    /// <summary>F8: a group or a category used only on the other platform ("Windows only"), shown greyed when the list shows other platforms.</summary>
    public bool IsElsewhere { get; init; }

    /// <summary>A short F8 note after the count: "Windows only", "no macOS name"; null for none.</summary>
    public string? Note { get; init; }

    /// <summary>What kind of section it is, before the count: "hold remap"; null for a group or a category.</summary>
    public string? KindText { get; init; }

    /// <summary>The hold remaps nested under an app group's section, counted after its commands ("1 hold remap").</summary>
    public int HoldRemapCount { get; init; }

    /// <summary>A section shown inside another one's (a hold remap in its app group).</summary>
    public bool IsNested => Id.Parent is not null;

    /// <summary>
    /// "3 commands", "1 command", "no commands", led by the <see cref="KindText"/> and followed by the hold remaps and the
    /// <see cref="Note"/> when there are some: "hold remap · 4 commands", "2 commands · 1 hold remap · Windows only".
    /// </summary>
    public string CountText
    {
        get
        {
            var parts = new List<string>(4);
            if (KindText is not null)
            {
                parts.Add(KindText);
            }

            parts.Add(Count(Commands.Count, "command"));
            if (HoldRemapCount > 0)
            {
                parts.Add(Count(HoldRemapCount, "hold remap"));
            }

            if (Note is not null)
            {
                parts.Add(Note);
            }

            return string.Join(" · ", parts);
        }
    }

    private static string Count(int count, string what) => count switch
    {
        0 => $"no {what}s",
        1 => $"1 {what}",
        var n => n.ToString(CultureInfo.InvariantCulture) + $" {what}s",
    };
}
