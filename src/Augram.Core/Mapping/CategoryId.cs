namespace Augram.Core.Mapping;

/// <summary>Strongly typed identity of a <see cref="CommandCategory"/>; stable across renames (F8: references by id, never by name).</summary>
public readonly record struct CategoryId(Guid Value)
{
    public static CategoryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
