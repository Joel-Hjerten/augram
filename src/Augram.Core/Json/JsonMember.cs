using System.Globalization;
using System.Text.Json.Nodes;

namespace Augram.Core.Json;

/// <summary>
/// The one reader of a hand-editable <see cref="JsonObject"/>'s members (plan 0007): the mapping subtree's
/// (<c>Config/JsonMembers</c>) and a step's parameters (<c>Steps/StepParameters</c>) are thin fronts for it, each passing
/// its own exception so its messages stay its own. A missing or JSON null member reads as absent (null); a present member
/// must have the right shape, else the front's exception is thrown. Enums are written by name, so a name is the only thing
/// read: one of the enum's names, matched case-insensitively, and nothing else (a number, even as a string such as
/// <c>"3"</c>, a name with spaces around it, or several names for an enum that is not a set of flags are refused; Joel,
/// 2026-10-11: Augram never writes them). A set of flags is its names separated by commas, spaces around each ignored; an
/// empty string, or empty entries, add nothing, and the zero name ("None") is a name like any other.
/// </summary>
internal static class JsonMember
{
    public static string? ReadString(JsonObject owner, string name, Func<Exception> wrongShape)
        => TryRead(owner, name, wrongShape, out string? text) ? text : null;

    public static bool? ReadBoolean(JsonObject owner, string name, Func<Exception> wrongShape)
        => TryRead(owner, name, wrongShape, out bool flag) ? flag : null;

    /// <summary>An integer; a string such as <c>"4"</c> is the wrong shape.</summary>
    public static int? ReadInt32(JsonObject owner, string name, Func<Exception> wrongShape)
        => TryRead(owner, name, wrongShape, out int number) ? number : null;

    public static JsonArray? ReadArray(JsonObject owner, string name, Func<Exception> wrongShape)
    {
        var node = owner[name];
        return node is null ? null : node as JsonArray ?? throw wrongShape();
    }

    /// <summary>A string member read as one of <typeparamref name="TEnum"/>'s names; <paramref name="notAName"/> gets the text it was not.</summary>
    public static TEnum? ReadEnum<TEnum>(JsonObject owner, string name, Func<Exception> wrongShape, Func<string, Exception> notAName)
        where TEnum : struct, Enum
    {
        var text = ReadString(owner, name, wrongShape);
        if (text is null)
        {
            return null;
        }

        return TryName<TEnum>(text, out var value) ? value : throw notAName(text);
    }

    /// <summary>A string member read as a set of <typeparamref name="TEnum"/>'s names separated by commas; <paramref name="notNames"/> gets the text when one is not a name.</summary>
    public static TEnum? ReadFlags<TEnum>(JsonObject owner, string name, Func<Exception> wrongShape, Func<string, Exception> notNames)
        where TEnum : struct, Enum
    {
        var text = ReadString(owner, name, wrongShape);
        if (text is null)
        {
            return null;
        }

        long bits = 0;
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            bits |= TryName<TEnum>(part, out var value) ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : throw notNames(text);
        }

        return (TEnum)Enum.ToObject(typeof(TEnum), bits);
    }

    /// <summary>The value of the enum name <paramref name="text"/> is, compared case-insensitively; false for anything else.</summary>
    private static bool TryName<TEnum>(string text, out TEnum value)
        where TEnum : struct, Enum
    {
        foreach (var member in Enum.GetNames<TEnum>())
        {
            if (string.Equals(member, text, StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse<TEnum>(member);
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryRead<T>(JsonObject owner, string name, Func<Exception> wrongShape, out T value)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var node = owner[name];
        if (node is null)
        {
            value = default!;
            return false;
        }

        if (node is JsonValue json && json.TryGetValue(out T? read))
        {
            value = read;
            return true;
        }

        throw wrongShape();
    }
}
