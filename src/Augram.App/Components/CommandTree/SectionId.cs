using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>
/// Identity of one section of the <see cref="CommandTree"/>: an app group (<see cref="CategoryId"/> null)
/// on the Apps tab, or a category of the Global group on the Global tab, where the Global group without a
/// category is <see cref="Uncategorized"/>. The Apps tab never shows the Global group, so the two readings
/// never meet in one tree. Stable across renames, so the selection and the expanded sections survive a rebuild.
/// </summary>
public readonly record struct SectionId(GroupId GroupId, CategoryId? CategoryId)
{
    /// <summary>The Global tab's section for Global commands without a category.</summary>
    public static SectionId Uncategorized { get; } = new(GroupId.Global, null);

    public static SectionId ForGroup(GroupId id) => new(id, null);

    public static SectionId ForCategory(GroupId group, CategoryId category) => new(group, category);
}
