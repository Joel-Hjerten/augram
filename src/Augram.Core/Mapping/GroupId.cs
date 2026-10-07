namespace Augram.Core.Mapping;

/// <summary>Strongly typed identity of an <see cref="AppGroup"/> or an <see cref="IgnoredApp"/>; stable across renames (F8).</summary>
public readonly record struct GroupId(Guid Value)
{
    /// <summary>
    /// The one group every document has (F5a: pinned first, never deleted). A fixed value, so every
    /// install, export and import agrees on which group is the Global one without looking at its name.
    /// </summary>
    public static GroupId Global { get; } = new(new Guid("00000000-0000-4000-8000-000000000001"));

    public static GroupId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
