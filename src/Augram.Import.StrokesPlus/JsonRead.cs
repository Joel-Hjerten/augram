using System.Text.Json;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// The tolerant member reads every reader shares: a member that is absent, null or of the wrong
/// kind reads as "not there" rather than throwing, because SP.net writes nulls freely and one odd
/// value must never fail a whole file.
/// </summary>
internal static class JsonRead
{
    /// <summary>The member as a trimmed string; empty when absent, null or not a string.</summary>
    public static string Text(JsonElement element, string member)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(member, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Trim()
            : string.Empty;

    /// <summary>The member as a bool: true for <c>true</c>, false for <c>false</c>, <paramref name="whenAbsent"/> for anything else.</summary>
    public static bool Flag(JsonElement element, string member, bool whenAbsent = false)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(member, out var value))
        {
            return whenAbsent;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => whenAbsent,
        };
    }

    public static bool TryArray(JsonElement element, string member, out JsonElement array)
        => TryKind(element, member, JsonValueKind.Array, out array);

    public static bool TryObject(JsonElement element, string member, out JsonElement value)
        => TryKind(element, member, JsonValueKind.Object, out value);

    /// <summary>A member of any kind, or an undefined element when absent.</summary>
    public static JsonElement Member(JsonElement element, string member)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(member, out var value) ? value : default;

    /// <summary>A parameter value as text: strings as is, numbers and bools in JSON form, objects and arrays as compact JSON, null as empty.</summary>
    public static string ValueText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Object or JsonValueKind.Array => JsonSerializer.Serialize(value),
        _ => string.Empty,
    };

    private static bool TryKind(JsonElement element, string member, JsonValueKind kind, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(member, out value) && value.ValueKind == kind)
        {
            return true;
        }

        value = default;
        return false;
    }
}
