using System.Text.Json;
using System.Text.Json.Serialization;
using Augram.Core.Gestures;

namespace Augram.Core.Config;

/// <summary>A <see cref="GestureId"/> is a plain Guid string in the file ("3f2a...-...").</summary>
internal sealed class GestureIdJsonConverter : JsonConverter<GestureId>
{
    public override GestureId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetGuid(out var value))
        {
            throw new JsonException("A gesture id must be a Guid string.");
        }

        return new GestureId(value);
    }

    public override void Write(Utf8JsonWriter writer, GestureId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
