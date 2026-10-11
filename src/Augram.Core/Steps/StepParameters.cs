using System.Text.Json.Nodes;
using Augram.Core.Json;

namespace Augram.Core.Steps;

/// <summary>
/// Reads one member of a step's parameter object the way every <see cref="IStepType.Read"/> does
/// (F8): the steps' front for <see cref="JsonMember"/>, the one member reader the config file's mapping reader shares. A
/// missing or null member yields null so the caller applies its default or demands it, a present member must have the right
/// shape, and every refusal is a <see cref="StepFormatException"/> whose message names the member. Enum names are matched
/// case-insensitively (as the config's enum strings are) and numeric strings are refused, so a hand edit cannot smuggle in
/// an unnamed value.
/// </summary>
public static class StepParameters
{
    /// <summary>The member as one of <typeparamref name="TEnum"/>'s names, or null when absent.</summary>
    public static TEnum? ReadEnum<TEnum>(JsonObject parameters, string name)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return JsonMember.ReadEnum<TEnum>(
            parameters,
            name,
            NotAString(name),
            text => new StepFormatException($"'{name}' must be one of {string.Join(", ", Enum.GetNames<TEnum>())}; got '{text}'."));
    }

    /// <summary>
    /// The member as a set of <typeparamref name="TEnum"/>'s names separated by commas ("Control, Shift"), case-insensitive;
    /// "None", an empty string and an absent member mean none. Every step's modifier set reads through this (Hotkey, Scroll,
    /// Remap), so a set reads the same in each.
    /// </summary>
    public static TEnum ReadFlags<TEnum>(JsonObject parameters, string name)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var names = string.Join(", ", Enum.GetNames<TEnum>().Where(member => member != "None"));
        return JsonMember.ReadFlags<TEnum>(
            parameters,
            name,
            NotAString(name),
            text => new StepFormatException($"'{name}' must list {names} separated by commas, or None; got '{text}'.")) ?? default;
    }

    /// <summary>The member as an integer within <paramref name="min"/>..<paramref name="max"/> inclusive, or null when absent.</summary>
    public static int? ReadInt32(JsonObject parameters, string name, int min, int max)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var number = JsonMember.ReadInt32(parameters, name, () => new StepFormatException($"'{name}' must be an integer."));
        if (number is { } value && (value < min || value > max))
        {
            throw new StepFormatException($"'{name}' must be between {min} and {max}; got {value}.");
        }

        return number;
    }

    /// <summary>The member as a string, or null when absent.</summary>
    public static string? ReadString(JsonObject parameters, string name)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return JsonMember.ReadString(parameters, name, NotAString(name));
    }

    /// <summary>The member as a JSON <c>true</c> or <c>false</c>, or null when absent; a string such as "true" is refused.</summary>
    public static bool? ReadBoolean(JsonObject parameters, string name)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return JsonMember.ReadBoolean(parameters, name, () => new StepFormatException($"'{name}' must be true or false."));
    }

    /// <summary>The exception <see cref="IStepType.Read"/> throws for a member the shape demands and the object lacks: "'width' is required when 'operation' is SetSize."</summary>
    public static StepFormatException Required(string name, string when)
        => new($"'{name}' is required when {when}.");

    /// <summary>The step handed to <see cref="IStepType.Write"/> or <see cref="IStepType.Execute"/> belongs to another type: a programming error, not a format error.</summary>
    public static TStep Expect<TStep>(IStep step, IStepType type)
        where TStep : class, IStep
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(type);
        return step as TStep
            ?? throw new ArgumentException($"The '{type.Key}' step type was handed a '{step.Type.Key}' step.", nameof(step));
    }

    private static Func<Exception> NotAString(string name) => () => new StepFormatException($"'{name}' must be a string.");
}
