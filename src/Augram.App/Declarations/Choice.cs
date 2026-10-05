namespace Augram.App.Declarations;

/// <summary>One option of a dropdown or button-radio field: what the user sees and what the binding gets.</summary>
public sealed record Choice<T>(string Label, T Value);

public static class Choice
{
    /// <summary>One choice per enum member, labelled by the member name.</summary>
    public static IReadOnlyList<Choice<T>> FromEnum<T>()
        where T : struct, Enum
        => [.. Enum.GetValues<T>().Select(value => new Choice<T>(value.ToString(), value))];

    public static IReadOnlyList<Choice<string>> FromStrings(params IReadOnlyList<string> values)
        => [.. values.Select(value => new Choice<string>(value, value))];
}
