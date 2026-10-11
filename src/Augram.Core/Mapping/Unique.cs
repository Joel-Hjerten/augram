namespace Augram.Core.Mapping;

/// <summary>
/// The uniqueness rule every list in the mapping keeps (app groups, excluded apps, a group's categories, hold remaps and
/// commands): an id once in the list, a name once among its siblings, compared with <see cref="MappingRules.NameComparer"/>.
/// Each rules class passes its own words, so the refusals read as before.
/// </summary>
internal static class Unique
{
    /// <summary>
    /// Walks <paramref name="others"/> in order: one with the item's id is refused with <paramref name="idTaken"/>; one among the
    /// item's siblings (every other unless <paramref name="isSibling"/> says otherwise) named <paramref name="name"/> is refused
    /// with <paramref name="nameTaken"/> of its stored name; then <paramref name="andThen"/> checks that sibling against the item
    /// (a hold key, a trigger), so that, other by other, the first broken rule is the one reported.
    /// </summary>
    /// <exception cref="MappingValidationException">A rule above is broken.</exception>
    public static void Ensure<T>(
        IEnumerable<T> others,
        Func<T, bool> hasId,
        Func<T, string> nameOf,
        string name,
        string idTaken,
        Func<string, string> nameTaken,
        Func<T, bool>? isSibling = null,
        Action<T>? andThen = null)
    {
        foreach (var other in others)
        {
            if (hasId(other))
            {
                throw new MappingValidationException(idTaken);
            }

            if (isSibling is not null && !isSibling(other))
            {
                continue;
            }

            if (MappingRules.NameComparer.Equals(nameOf(other), name))
            {
                throw new MappingValidationException(nameTaken(nameOf(other)));
            }

            andThen?.Invoke(other);
        }
    }
}
