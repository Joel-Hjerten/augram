using System.Text.Json;
using System.Text.Json.Serialization;

namespace Augram.Core.Config;

/// <summary>
/// <c>{ "theme": "Dark", "windowBackground": "FrostedGlass", "tintPercent": 62, "cornerRadiusPx": 12, "accentFollowsTrail": true, "accent": "#F5C542" }</c>.
/// Hand-written for the same reasons as <see cref="TrailSettingsJsonConverter"/>: a member missing from the file keeps its
/// default, and the accent is one hex string. Enums are their names; a name the enum lacks is a format error naming the
/// member, as for every enum in the file. Range checks are <see cref="SettingsRules"/>'. Unknown members are skipped.
/// </summary>
internal sealed class AppearanceSettingsJsonConverter : JsonConverter<AppearanceSettings>
{
    private const string ThemeMember = "theme";
    private const string WindowBackgroundMember = "windowBackground";
    private const string TintPercentMember = "tintPercent";
    private const string CornerRadiusPxMember = "cornerRadiusPx";
    private const string AccentFollowsTrailMember = "accentFollowsTrail";
    private const string AccentMember = "accent";

    public override AppearanceSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("'appearance' must be an object.");
        }

        var appearance = AppearanceSettings.Default;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (Is(name, ThemeMember))
            {
                appearance = appearance with { Theme = ReadEnum<AppTheme>(ref reader, ThemeMember) };
            }
            else if (Is(name, WindowBackgroundMember))
            {
                appearance = appearance with { WindowBackground = ReadEnum<WindowBackground>(ref reader, WindowBackgroundMember) };
            }
            else if (Is(name, TintPercentMember))
            {
                appearance = appearance with { TintPercent = ReadInt(ref reader, TintPercentMember) };
            }
            else if (Is(name, CornerRadiusPxMember))
            {
                appearance = appearance with { CornerRadiusPx = ReadInt(ref reader, CornerRadiusPxMember) };
            }
            else if (Is(name, AccentFollowsTrailMember))
            {
                appearance = appearance with { AccentFollowsTrail = ReadBool(ref reader, AccentFollowsTrailMember) };
            }
            else if (Is(name, AccentMember))
            {
                appearance = appearance with { Accent = ReadColour(ref reader) };
            }
            else
            {
                reader.Skip();
            }
        }

        return appearance;
    }

    public override void Write(Utf8JsonWriter writer, AppearanceSettings value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(ThemeMember, value.Theme.ToString());
        writer.WriteString(WindowBackgroundMember, value.WindowBackground.ToString());
        writer.WriteNumber(TintPercentMember, value.TintPercent);
        writer.WriteNumber(CornerRadiusPxMember, value.CornerRadiusPx);
        writer.WriteBoolean(AccentFollowsTrailMember, value.AccentFollowsTrail);
        writer.WriteString(AccentMember, value.Accent.ToString());
        writer.WriteEndObject();
    }

    private static bool Is(string? name, string member) => string.Equals(name, member, StringComparison.OrdinalIgnoreCase);

    private static T ReadEnum<T>(ref Utf8JsonReader reader, string member)
        where T : struct, Enum
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<T>(reader.GetString(), ignoreCase: true, out var value)
            && Enum.IsDefined(value))
        {
            return value;
        }

        throw new JsonException($"'{member}' must be one of {string.Join(", ", Enum.GetNames<T>())}.");
    }

    private static int ReadInt(ref Utf8JsonReader reader, string member)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value))
        {
            return value;
        }

        throw new JsonException($"'{member}' must be a whole number.");
    }

    private static bool ReadBool(ref Utf8JsonReader reader, string member)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
        {
            return reader.GetBoolean();
        }

        throw new JsonException($"'{member}' must be true or false.");
    }

    private static RgbColor ReadColour(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String && RgbColor.TryParse(reader.GetString(), out var colour))
        {
            return colour;
        }

        throw new JsonException($"'{AccentMember}' must be a \"#RRGGBB\" string.");
    }
}
