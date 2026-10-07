using System.Text.Json.Nodes;

namespace Augram.Core.Steps;

/// <summary>
/// Reads one member of a step's parameter object the way every <see cref="IStepType.Read"/> does
/// (F8): a missing or null member yields null so the caller applies its default or demands it, a
/// present member must have the right shape, and every refusal is a <see cref="StepFormatException"/>
/// whose message names the member. Enum names are matched case-insensitively (as the config's
/// enum strings are) and numeric strings are refused, so a hand edit cannot smuggle in an unnamed value.
/// </summary>
public static class StepParameters
{
    /// <summary>The member as one of <typeparamref name="TEnum"/>'s names, or null when absent.</summary>
    public static TEnum? ReadEnum<TEnum>(JsonObject parameters, string name)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var text = ReadString(parameters, name);
        if (text is null)
        {
            return null;
        }

        foreach (var member in Enum.GetNames<TEnum>())
        {
            if (string.Equals(member, text, StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse<TEnum>(member);
            }
        }

        throw new StepFormatException($"'{name}' must be one of {string.Join(", ", Enum.GetNames<TEnum>())}; got '{text}'.");
    }

    /// <summary>The member as an integer within <paramref name="min"/>..<paramref name="max"/> inclusive, or null when absent.</summary>
    public static int? ReadInt32(JsonObject parameters, string name, int min, int max)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var node = parameters[name];
        if (node is null)
        {
            return null;
        }

        if (node is not JsonValue value || !value.TryGetValue(out int number))
        {
            throw new StepFormatException($"'{name}' must be an integer.");
        }

        if (number < min || number > max)
        {
            throw new StepFormatException($"'{name}' must be between {min} and {max}; got {number}.");
        }

        return number;
    }

    /// <summary>The member as a string, or null when absent.</summary>
    public static string? ReadString(JsonObject parameters, string name)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var node = parameters[name];
        if (node is null)
        {
            return null;
        }

        if (node is not JsonValue value || !value.TryGetValue(out string? text))
        {
            throw new StepFormatException($"'{name}' must be a string.");
        }

        return text;
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
}
