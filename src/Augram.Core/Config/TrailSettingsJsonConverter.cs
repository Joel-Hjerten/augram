using System.Text.Json;
using System.Text.Json.Serialization;

namespace Augram.Core.Config;

/// <summary>
/// <c>{ "widthPx": 5, "opacity": 0.5, "colour": "#00FF40" }</c>. Hand-written so a member
/// missing from the file keeps its default (the source generator would zero it) and the
/// colour is one hex string rather than three numbers. Unknown members are skipped.
/// </summary>
internal sealed class TrailSettingsJsonConverter : JsonConverter<TrailSettings>
{
    private const string WidthPx = "widthPx";
    private const string Opacity = "opacity";
    private const string Colour = "colour";

    public override TrailSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("'trail' must be an object.");
        }

        var trail = TrailSettings.Default;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            if (string.Equals(name, WidthPx, StringComparison.OrdinalIgnoreCase))
            {
                trail = trail with { WidthPx = reader.GetDouble() };
            }
            else if (string.Equals(name, Opacity, StringComparison.OrdinalIgnoreCase))
            {
                trail = trail with { Opacity = reader.GetDouble() };
            }
            else if (string.Equals(name, Colour, StringComparison.OrdinalIgnoreCase))
            {
                trail = trail with { Colour = ReadColour(ref reader) };
            }
            else
            {
                reader.Skip();
            }
        }

        return trail;
    }

    public override void Write(Utf8JsonWriter writer, TrailSettings value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(WidthPx, value.WidthPx);
        writer.WriteNumber(Opacity, value.Opacity);
        writer.WriteString(Colour, value.Colour.ToString());
        writer.WriteEndObject();
    }

    private static RgbColor ReadColour(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.String && RgbColor.TryParse(reader.GetString(), out var colour))
        {
            return colour;
        }

        throw new JsonException("'colour' must be a \"#RRGGBB\" string.");
    }
}
