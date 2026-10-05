namespace Augram.Core.Gestures;

/// <summary>Strongly typed identity of a <see cref="Gesture"/>; stable across renames.</summary>
public readonly record struct GestureId(Guid Value)
{
    public static GestureId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
