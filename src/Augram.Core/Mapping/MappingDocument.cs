namespace Augram.Core.Mapping;

/// <summary>
/// The whole mapping (F5, F5a): the app groups, Global first and then by name, and the ignored apps.
/// An immutable snapshot; <see cref="MappingStore"/> replaces it wholesale and the engine resolves
/// against whichever snapshot it was last handed. Build one through the store or
/// <see cref="MappingRules.ValidDocument"/>; a raw instance (straight from the file, say) may be unsorted or invalid.
/// </summary>
public sealed record MappingDocument(IReadOnlyList<AppGroup> Groups, IReadOnlyList<IgnoredApp> Ignored)
{
    /// <summary>Just the Global group, active and empty.</summary>
    public static MappingDocument Empty { get; } = new([AppGroup.EmptyGlobal], []);

    /// <summary>The Global group; first in a normalised document.</summary>
    /// <exception cref="InvalidOperationException">No group has <see cref="GroupId.Global"/>: the document was never validated.</exception>
    public AppGroup Global
        => Groups.FirstOrDefault(group => group.IsGlobal)
            ?? throw new InvalidOperationException("The mapping has no Global group; validate it first.");

    /// <summary>Every command with its group, in document order.</summary>
    public IEnumerable<(AppGroup Group, Command Command)> AllCommands()
    {
        foreach (var group in Groups)
        {
            foreach (var command in group.Commands)
            {
                yield return (group, command);
            }
        }
    }
}
