using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Config;

/// <summary>
/// Text in, <see cref="SyncFile"/> out, and back (README: sync file). The <c>gestures</c> and <c>mapping</c>
/// members go through exactly the config file's code (<see cref="ConfigJsonContext"/>,
/// <see cref="MappingJsonWriter"/>, <see cref="MappingJsonReader"/>), so the two never drift; the schema
/// version is the config file's and is checked and migrated the same way. The sync format version
/// (<see cref="SyncFile.CurrentFormatVersion"/>) is the sync's own; <see cref="ReadHeader"/> reads both before
/// anything else, and <see cref="Read"/> refuses a file newer in either. A file read here is validated by
/// <see cref="GestureRules"/> and <see cref="MappingRules"/> and comes back normalised. <see cref="TryRead"/>
/// is the tolerant entry point the sync uses: a broken file is an error line, never an exception.
/// </summary>
public static class SyncFileSerializer
{
    private const string What = "The sync file";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    public static string Write(SyncFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ConfigDocument.CurrentSchemaVersion);
            writer.WriteNumber("formatVersion", SyncFile.CurrentFormatVersion);
            writer.WriteStartObject("machine");
            writer.WriteString("id", file.MachineId);
            writer.WriteString("name", file.MachineName);
            writer.WriteString("writtenAt", file.WrittenAt);
            writer.WriteString("revision", file.Revision);
            writer.WriteEndObject();
            writer.WriteStartArray("merged");
            foreach (var entry in file.Merged.OrderBy(entry => entry.MachineId))
            {
                WriteAcknowledgement(writer, entry);
            }

            writer.WriteEndArray();
            writer.WritePropertyName("gestures");
            JsonSerializer.Serialize(writer, file.Gestures, ConfigJsonContext.Default.IReadOnlyListGesture);
            writer.WritePropertyName("mapping");
            MappingJsonWriter.Write(writer, file.Mapping);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>The file's versions and machine name, or null when it is not a JSON object with readable versions (<see cref="TryRead"/> then says why).</summary>
    public static SyncFileHeader? ReadHeader(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            var root = ConfigSerializer.ParseObject(json, What);
            if (root["schemaVersion"] is not JsonValue schema || !schema.TryGetValue(out int schemaVersion))
            {
                return null;
            }

            string? name = root["machine"] is JsonObject machine && machine["name"] is JsonValue value && value.TryGetValue(out string? text) ? text : null;
            return new SyncFileHeader(schemaVersion, ReadFormatVersion(root), name);
        }
        catch (ConfigFormatException)
        {
            return null;
        }
    }

    /// <summary>Reads, validates and normalises; every problem is the returned <paramref name="error"/> line, nothing throws.</summary>
    /// <param name="json">The file's text.</param>
    /// <param name="steps">The step types the mapping may use.</param>
    /// <param name="notice">Receives one line per step dropped on the way (an unknown type, say); the caller decides whether that is acceptable.</param>
    /// <param name="file">The file, when it could be read.</param>
    /// <param name="error">Why it could not, one line.</param>
    public static bool TryRead(string json, StepRegistry steps, Action<string>? notice, [NotNullWhen(true)] out SyncFile? file, [NotNullWhen(false)] out string? error)
    {
        try
        {
            file = Read(json, steps, notice);
            error = null;
            return true;
        }
        catch (ConfigFormatException ex)
        {
            file = null;
            error = ex.Message;
            return false;
        }
    }

    /// <exception cref="ConfigFormatException">Malformed, newer than this build (config schema or sync format), or breaking a gesture or mapping rule.</exception>
    public static SyncFile Read(string json, StepRegistry steps, Action<string>? notice)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(steps);

        var root = ConfigSerializer.ParseObject(json, What);
        int version = ConfigSerializer.ReadSchemaVersion(root, What);
        if (version < ConfigDocument.CurrentSchemaVersion)
        {
            root = ConfigMigrations.Migrate(root, version);
        }

        int format = ReadFormatVersion(root);
        if (format > SyncFile.CurrentFormatVersion)
        {
            throw new ConfigFormatException(
                $"{What} was written by a newer Augram (sync format {format}); this build reads up to format {SyncFile.CurrentFormatVersion}.");
        }

        var machine = JsonMembers.RequireObject(root["machine"], "'machine' of the sync file");
        const string where = "the sync file's 'machine'";
        var merged = JsonMembers.OptionalArray(root, "merged", "the sync file").Select(ReadAcknowledgement).ToArray();
        var gestures = ReadGestures(root["gestures"]);
        var mapping = new MappingJsonReader(steps, notice).Read(root["mapping"]);

        try
        {
            return new SyncFile(
                ConfigDocument.CurrentSchemaVersion,
                JsonMembers.RequireGuid(machine, "id", where),
                JsonMembers.RequireString(machine, "name", where),
                ReadTimestamp(machine, where),
                GestureRules.ValidSet(gestures),
                MappingRules.ValidDocument(mapping))
            {
                FormatVersion = format,
                Revision = JsonMembers.RequireGuid(machine, "revision", where),
                Merged = merged,
            };
        }
        catch (Exception ex) when (ex is GestureValidationException or MappingValidationException)
        {
            throw new ConfigFormatException($"{What} breaks a rule: {ex.Message}", ex);
        }
    }

    /// <summary>The sync format; 1 when the member is missing (every file written before it existed).</summary>
    private static int ReadFormatVersion(JsonObject root)
    {
        if (root["formatVersion"] is not { } node)
        {
            return 1;
        }

        return node is JsonValue value && value.TryGetValue(out int format) && format >= 1
            ? format
            : throw new ConfigFormatException("'formatVersion' of the sync file must be an integer of 1 or more.");
    }

    private static List<Gesture> ReadGestures(JsonNode? node)
    {
        if (node is null)
        {
            return [];
        }

        try
        {
            var gestures = node.Deserialize(ConfigJsonContext.Default.IReadOnlyListGesture) ?? [];
            ConfigSerializer.EnsureComplete(gestures);
            return [.. gestures];
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException($"The gestures of the sync file could not be read: {ex.Message}", ex);
        }
    }

    private static DateTimeOffset ReadTimestamp(JsonObject machine, string where)
    {
        var text = JsonMembers.RequireString(machine, "writtenAt", where);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
            ? when
            : throw new ConfigFormatException($"'writtenAt' of {where} must be an ISO 8601 date and time.");
    }

    private static void WriteAcknowledgement(Utf8JsonWriter writer, SyncAcknowledgement entry)
    {
        writer.WriteStartObject();
        writer.WriteString("machineId", entry.MachineId);
        writer.WriteString("revision", entry.Revision);
        WriteKeys(writer, "except", entry.Except);
        WriteKeys(writer, "pending", entry.Pending);
        writer.WriteEndObject();
    }

    private static void WriteKeys(Utf8JsonWriter writer, string name, IReadOnlyList<string> keys)
    {
        writer.WriteStartArray(name);
        foreach (var key in keys.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(key);
        }

        writer.WriteEndArray();
    }

    private static SyncAcknowledgement ReadAcknowledgement(JsonNode? node)
    {
        const string where = "an entry of the sync file's 'merged'";
        var entry = JsonMembers.RequireObject(node, "An entry of the sync file's 'merged'");
        return new SyncAcknowledgement(
            JsonMembers.RequireGuid(entry, "machineId", where),
            JsonMembers.RequireGuid(entry, "revision", where),
            JsonMembers.OptionalStrings(entry, "except", where),
            JsonMembers.OptionalStrings(entry, "pending", where));
    }
}
