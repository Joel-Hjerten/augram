using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Augram.Core.Gestures;

namespace Augram.Core.Config;

/// <summary>
/// A gesture's samples: an array of samples, each an array of <c>[x, y]</c> arrays with one
/// sample per line (<c>[[50,100],[50,0]]</c>) so an indented file stays diffable without one
/// coordinate per line. Written as raw JSON because <see cref="Utf8JsonWriter"/> would
/// otherwise indent every number. Doubles round-trip exactly (shortest round-trippable form).
/// </summary>
internal sealed class GestureSamplesJsonConverter : JsonConverter<IReadOnlyList<GestureSample>>
{
    public override IReadOnlyList<GestureSample> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("'samples' must be an array of samples.");
        }

        var samples = new List<GestureSample>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            samples.Add(ReadSample(ref reader));
        }

        return samples;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<GestureSample> value, JsonSerializerOptions options)
    {
        if (!options.WriteIndented || value.Count == 0)
        {
            writer.WriteRawValue("[" + string.Join(",", value.Select(Compact)) + "]");
            return;
        }

        var inner = Indent(options, writer.CurrentDepth + 1);
        var outer = Indent(options, writer.CurrentDepth);
        var lines = string.Join("," + options.NewLine, value.Select(sample => inner + Compact(sample)));
        writer.WriteRawValue("[" + options.NewLine + lines + options.NewLine + outer + "]");
    }

    private static string Indent(JsonSerializerOptions options, int depth)
        => new(options.IndentCharacter, options.IndentSize * depth);

    private static string Compact(GestureSample sample)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var compact = new Utf8JsonWriter(buffer))
        {
            compact.WriteStartArray();
            foreach (var point in sample)
            {
                compact.WriteStartArray();
                compact.WriteNumberValue(point.X);
                compact.WriteNumberValue(point.Y);
                compact.WriteEndArray();
            }

            compact.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static GestureSample ReadSample(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("A gesture sample must be an array of [x, y] points.");
        }

        var points = new List<GesturePoint>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            points.Add(ReadPoint(ref reader));
        }

        return new GestureSample(points);
    }

    private static GesturePoint ReadPoint(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("A point must be an [x, y] array.");
        }

        double x = ReadCoordinate(ref reader);
        double y = ReadCoordinate(ref reader);
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("A point must have exactly two numbers.");
        }

        return new GesturePoint(x, y);
    }

    private static double ReadCoordinate(ref Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException("A point coordinate must be a number.");
        }

        return reader.GetDouble();
    }
}
