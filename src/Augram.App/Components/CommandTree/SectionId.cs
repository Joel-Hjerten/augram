using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Identity of one section of the <see cref="CommandTree"/>: an app group (<see cref="CategoryId"/> and
/// <see cref="HoldRemapId"/> null) on the Apps tab, a hold remap of an app group there (F9, plan 0002: a section nested in
/// its group's, <see cref="Parent"/>), or a category of the Global group on the Global tab, where the Global group without a
/// category is <see cref="Uncategorized"/>. The Apps tab never shows the Global group, so the readings never meet in one
/// tree. Stable across renames, so the selection and the expanded sections survive a rebuild.
/// </summary>
public readonly record struct SectionId(GroupId GroupId, CategoryId? CategoryId, HoldRemapId? HoldRemapId = null)
{
    /// <summary>The Global tab's section for Global commands without a category.</summary>
    public static SectionId Uncategorized { get; } = new(GroupId.Global, null);

    /// <summary>The section a nested section sits in: a hold remap's group; null for a top-level section.</summary>
    public SectionId? Parent => HoldRemapId is null ? null : ForGroup(GroupId);

    public static SectionId ForGroup(GroupId id) => new(id, null);

    public static SectionId ForCategory(GroupId group, CategoryId category) => new(group, category);

    public static SectionId ForHoldRemap(GroupId group, HoldRemapId holdRemap) => new(group, null, holdRemap);
}
