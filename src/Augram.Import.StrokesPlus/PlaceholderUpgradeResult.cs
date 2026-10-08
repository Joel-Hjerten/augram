using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// What <see cref="PlaceholderUpgrade.Upgrade"/> did: the mapping to commit (the same instance when nothing changed) and how
/// many placeholders of each SP.net method it replaced.
/// </summary>
public sealed record PlaceholderUpgradeResult(MappingDocument Mapping, IReadOnlyDictionary<string, int> Methods)
{
    /// <summary>Placeholders replaced in all; 0 means there is nothing to commit.</summary>
    public int Count => Methods.Values.Sum();
}
