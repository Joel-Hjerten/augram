using System.Text.Json;
using System.Text.Json.Nodes;

namespace Augram.Core.Config;

/// <summary>
/// Text in, <see cref="ConfigDocument"/> out, and back. <see cref="Write"/> always emits the
/// current schema version. <see cref="Read"/> checks the version first (older: migrate;
/// newer: refuse) and turns every parse problem into a <see cref="ConfigFormatException"/>
/// whose message says what is wrong, so the store can report it and fall back.
/// </summary>
public static class ConfigSerializer
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static string Write(ConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var current = document.SchemaVersion == ConfigDocument.CurrentSchemaVersion
            ? document
            : document with { SchemaVersion = ConfigDocument.CurrentSchemaVersion };

        return JsonSerializer.Serialize(current, ConfigJsonContext.Default.ConfigDocument);
    }

    public static ConfigDocument Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

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
            return Validated(document);
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
