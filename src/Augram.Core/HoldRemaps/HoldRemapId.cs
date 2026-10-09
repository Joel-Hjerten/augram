namespace Augram.Core.HoldRemaps;

/// <summary>
/// Strongly typed identity of a <see cref="HoldRemap"/>; stable across renames and a change of hold key (F8: references by
/// id, never by name). Unique within its app group, as a category's is (a sync item key carries the group's id too).
/// </summary>
public readonly record struct HoldRemapId(Guid Value)
{
    public static HoldRemapId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
