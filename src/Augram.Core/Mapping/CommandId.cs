namespace Augram.Core.Mapping;

/// <summary>Strongly typed identity of a <see cref="Command"/>; stable across renames and moves between groups (F8).</summary>
public readonly record struct CommandId(Guid Value)
{
    public static CommandId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
