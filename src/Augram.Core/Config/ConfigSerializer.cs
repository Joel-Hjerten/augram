using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Augram.Core.Steps;

namespace Augram.Core.Config;

/// <summary>
/// Text in, <see cref="ConfigDocument"/> out, and back. <see cref="Write(ConfigDocument)"/> always
/// emits the current schema version. <see cref="Read(string)"/> checks the version first (older:
/// migrate; newer: refuse) and turns every parse problem into a <see cref="ConfigFormatException"/>
/// whose message says what is wrong, so the store can report it and fall back. The top-level
/// members go through the generated <see cref="ConfigJsonContext"/>, except <c>mapping</c>, whose
/// steps are polymorphic: <see cref="MappingJsonWriter"/> and <see cref="MappingJsonReader"/> handle
/// it, which is why the envelope is written by hand here (its names are the record's, camelCased).
/// </summary>
public static class ConfigSerializer
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    public static string Write(ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ConfigDocument.CurrentSchemaVersion);
            writer.WritePropertyName("settings");
            JsonSerializer.Serialize(writer, document.Settings, ConfigJsonContext.Default.Settings);
            writer.WritePropertyName("gestures");
            JsonSerializer.Serialize(writer, document.Gestures, ConfigJsonContext.Default.IReadOnlyListGesture);
            writer.WritePropertyName("mapping");
            MappingJsonWriter.Write(writer, document.Mapping);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Reads with the built-in step types; a step of an unknown type is dropped silently. Hosts pass a notice sink through the other overload.</summary>
    public static ConfigDocument Read(string json) => Read(json, StepRegistry.BuiltIn, notice: null);

    /// <param name="json">The file's text.</param>
    /// <param name="steps">The step types the mapping may use; a step of another type, or one its type refuses, is dropped (F8).</param>
    /// <param name="notice">Receives one line per dropped step or override; null to drop silently.</param>
    public static ConfigDocument Read(string json, StepRegistry steps, Action<string>? notice)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(steps);

        var root = ParseObject(json);
        int version = ReadSchemaVersion(root);
        if (version < ConfigDocument.CurrentSchemaVersion)
        {
            root = ConfigMigrations.Migrate(root, version);
        }

        try
        {
            var document = root.Deserialize(ConfigJsonContext.Default.ConfigDocument)
                ?? throw new ConfigFormatException("The configuration is empty.");
            var mapping = new MappingJsonReader(steps, notice).Read(root["mapping"]);
            return Validated(document with { Mapping = mapping });
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException($"The configuration could not be read: {ex.Message}", ex);
        }
    }

    private static JsonObject ParseObject(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: ParseOptions);
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException($"The configuration is not valid JSON: {ex.Message}", ex);
        }

        return node as JsonObject
            ?? throw new ConfigFormatException("The configuration must be a JSON object at the top level.");
    }

    private static int ReadSchemaVersion(JsonObject root)
    {
        var node = root["schemaVersion"]
            ?? throw new ConfigFormatException("The configuration has no 'schemaVersion' field.");

        if (node is not JsonValue value || !value.TryGetValue(out int version))
        {
            throw new ConfigFormatException("'schemaVersion' must be an integer.");
        }

        if (version < 1)
        {
            throw new ConfigFormatException($"'schemaVersion' {version} is not valid; the first version is 1.");
        }

        if (version > ConfigDocument.CurrentSchemaVersion)
        {
            throw new ConfigFormatException(
                $"The configuration was written by a newer Augram (schema version {version}); this build reads up to version {ConfigDocument.CurrentSchemaVersion}.");
        }

        return version;
    }

    /// <summary>Missing or null settings sections took defaults in the constructors; gestures are strict.</summary>
    private static ConfigDocument Validated(ConfigDocument document)
    {
        foreach (var gesture in document.Gestures)
        {
            if (gesture is null || gesture.Name is null || gesture.Samples is null || gesture.Samples.Any(sample => sample is null))
            {
                throw new ConfigFormatException("Every gesture needs 'id', 'name' and a 'samples' array without nulls.");
            }
        }

        return document;
    }
}
