using System.Globalization;
using System.Text.Json.Nodes;

namespace Augram.Core.Config;

/// <summary>
/// Typed access to the members of a hand-editable <see cref="JsonObject"/>, for the mapping subtree
/// that <see cref="ConfigJsonContext"/> does not cover: a missing or null member takes the fallback
/// the caller passes (or is demanded), a present member must have the right shape, and every refusal
/// is a <see cref="ConfigFormatException"/> that names the member and where it sits
/// ("'id' of app group 'Chrome' must be a Guid string.").
/// </summary>
internal static class JsonMembers
{
    public static JsonObject RequireObject(JsonNode? node, string what)
        => node as JsonObject ?? throw new ConfigFormatException($"{what} must be a JSON object.");

    /// <summary>Missing or null → an empty array.</summary>
    public static JsonArray OptionalArray(JsonObject owner, string name, string where)
    {
        var node = owner[name];
        return node is null ? [] : node as JsonArray ?? throw Refuse(name, where, "an array");
    }

    public static string RequireString(JsonObject owner, string name, string where)
        => OptionalString(owner, name, where) ?? throw new ConfigFormatException($"'{name}' of {where} is required.");

    public static string? OptionalString(JsonObject owner, string name, string where)
    {
        var node = owner[name];
        if (node is null)
        {
            return null;
        }

        return node is JsonValue value && value.TryGetValue(out string? text) ? text : throw Refuse(name, where, "a string");
    }

    /// <summary>The member as a string, or null when it is missing, null or not a string: for members a reader drops rather than refuses.</summary>
    public static string? TryString(JsonObject owner, string name)
        => owner[name] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    /// <summary>The member as a Guid, or null when it is missing or not a Guid string.</summary>
    public static Guid? TryGuid(JsonObject owner, string name)
        => Guid.TryParse(TryString(owner, name), out var guid) ? guid : null;

    public static bool OptionalBool(JsonObject owner, string name, bool fallback, string where)
    {
        var node = owner[name];
        if (node is null)
        {
            return fallback;
        }

        return node is JsonValue value && value.TryGetValue(out bool flag) ? flag : throw Refuse(name, where, "true or false");
    }

    /// <summary>Missing or null → the fallback; otherwise an integer (a string such as "180" is refused).</summary>
    public static int OptionalInt32(JsonObject owner, string name, int fallback, string where)
        => OptionalInt32(owner, name, where) ?? fallback;

    /// <summary>Missing or null → null; otherwise an integer (a string such as "4" is refused).</summary>
    public static int? OptionalInt32(JsonObject owner, string name, string where)
    {
        var node = owner[name];
        if (node is null)
        {
            return null;
        }

        return node is JsonValue value && value.TryGetValue(out int number) ? number : throw Refuse(name, where, "an integer");
    }

    public static Guid RequireGuid(JsonObject owner, string name, string where)
    {
        var text = RequireString(owner, name, where);
        return Guid.TryParse(text, out var guid) ? guid : throw Refuse(name, where, "a Guid string");
    }

    /// <summary>Missing or null → empty; otherwise an array of strings.</summary>
    public static IReadOnlyList<string> OptionalStrings(JsonObject owner, string name, string where)
    {
        var array = OptionalArray(owner, name, where);
        var strings = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is not JsonValue value || !value.TryGetValue(out string? text))
            {
                throw Refuse(name, where, "an array of strings");
            }

            strings.Add(text);
        }

        return strings;
    }

    /// <summary>Missing or null → the fallback; otherwise one of the enum's names, matched case-insensitively like the file's other enums.</summary>
    public static TEnum OptionalEnum<TEnum>(JsonObject owner, string name, TEnum fallback, string where)
        where TEnum : struct, Enum
    {
        var text = OptionalString(owner, name, where);
        if (text is null)
        {
            return fallback;
        }

        return Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw Refuse(name, where, $"one of {string.Join(", ", Enum.GetNames<TEnum>())}");
    }

    /// <summary>A flags enum written by name, comma-separated ("Stroke, Right"); missing or null is <paramref name="fallback"/>, a name or bit the enum lacks is refused.</summary>
    public static TEnum OptionalFlags<TEnum>(JsonObject owner, string name, TEnum fallback, string where)
        where TEnum : struct, Enum
    {
        var text = OptionalString(owner, name, where);
        if (text is null)
        {
            return fallback;
        }

        long defined = 0;
        foreach (var value in Enum.GetValues<TEnum>())
        {
            defined |= Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }

        return Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && (Convert.ToInt64(parsed, CultureInfo.InvariantCulture) & ~defined) == 0
            ? parsed
            : throw Refuse(name, where, $"a comma-separated list of {string.Join(", ", Enum.GetNames<TEnum>())}");
    }

    private static ConfigFormatException Refuse(string name, string where, string expected)
        => new($"'{name}' of {where} must be {expected}.");
}
