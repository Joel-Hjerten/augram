using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Augram.Core.Config;

namespace Augram.Core.Sync;

/// <summary>
/// The JSON of the files under <c>sync/state/</c> (<see cref="SyncBaseStore"/>): one per other machine and one per
/// published revision. Item contents are stored as JSON strings keyed by the item key's text form, sorted, so a
/// diff of two states is readable. Every read failure is a <see cref="ConfigFormatException"/>.
/// </summary>
internal static class SyncStateJson
{
    // Relaxed escaping keeps the stored item JSON readable (a quote stays a backslash-quote); these files are never embedded in HTML.
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Write(SyncMachineState state) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("machineId", state.MachineId);
        writer.WriteString("machineName", state.MachineName);
        WriteGuid(writer, "mergedRevision", state.MergedRevision);
        WriteGuid(writer, "appliedAcknowledgement", state.AppliedAcknowledgement);
        if (state.LastMerged is { } lastMerged)
        {
            writer.WriteString("lastMerged", lastMerged);
        }

        WriteKeys(writer, "held", state.Held.Select(key => key.ToString()));
        writer.WriteStartArray("conflicts");
        foreach (var conflict in state.Conflicts)
        {
            writer.WriteStartObject();
            writer.WriteString("key", conflict.Key.ToString());
            writer.WriteString("name", conflict.Name);
            writer.WriteString("local", conflict.LocalContent);
            writer.WriteString("remote", conflict.RemoteContent);
            writer.WriteString("detectedAt", conflict.DetectedAt);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteContents(writer, "base", state.Base);
        writer.WriteEndObject();
    });

    public static SyncMachineState ReadMachine(string json)
    {
        var root = ConfigSerializer.ParseObject(json, "A sync state file");
        const string where = "a sync state file";
        var id = JsonMembers.RequireGuid(root, "machineId", where);
        var name = JsonMembers.RequireString(root, "machineName", where);
        return new SyncMachineState(id, name)
        {
            MergedRevision = JsonMembers.TryGuid(root, "mergedRevision"),
            AppliedAcknowledgement = JsonMembers.TryGuid(root, "appliedAcknowledgement"),
            LastMerged = JsonMembers.OptionalString(root, "lastMerged", where) is { } text ? Timestamp(text, where) : null,
            Held = JsonMembers.OptionalStrings(root, "held", where).Select(text => Key(text, where)).ToArray(),
            Conflicts = JsonMembers.OptionalArray(root, "conflicts", where).Select(node => ReadConflict(node, id, name)).ToArray(),
            Base = ReadContents(root, "base", where),
        };
    }

    public static string Write(SyncPublished published) => Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("revision", published.Revision);
        writer.WriteString("writtenAt", published.WrittenAt);
        writer.WriteStartArray("merged");
        foreach (var entry in published.Merged.OrderBy(entry => entry.MachineId))
        {
            writer.WriteStartObject();
            writer.WriteString("machineId", entry.MachineId);
            writer.WriteString("revision", entry.Revision);
            WriteKeys(writer, "except", entry.Except);
            WriteKeys(writer, "pending", entry.Pending);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteContents(writer, "items", published.Items);
        writer.WriteEndObject();
    });

    public static SyncPublished ReadPublished(string json)
    {
        var root = ConfigSerializer.ParseObject(json, "A published sync revision");
        const string where = "a published sync revision";
        var merged = JsonMembers.OptionalArray(root, "merged", where).Select(node =>
        {
            var entry = JsonMembers.RequireObject(node, "An entry of 'merged'");
            return new SyncAcknowledgement(
                JsonMembers.RequireGuid(entry, "machineId", where),
                JsonMembers.RequireGuid(entry, "revision", where),
                JsonMembers.OptionalStrings(entry, "except", where),
                JsonMembers.OptionalStrings(entry, "pending", where));
        }).ToArray();

        return new SyncPublished(
            JsonMembers.RequireGuid(root, "revision", where),
            Timestamp(JsonMembers.RequireString(root, "writtenAt", where), where),
            ReadContents(root, "items", where),
            merged);
    }

    private static SyncConflict ReadConflict(JsonNode? node, Guid machineId, string machineName)
    {
        const string where = "a pending sync conflict";
        var conflict = JsonMembers.RequireObject(node, "A pending sync conflict");
        return new SyncConflict(
            Key(JsonMembers.RequireString(conflict, "key", where), where),
            JsonMembers.RequireString(conflict, "name", where),
            JsonMembers.OptionalString(conflict, "local", where),
            JsonMembers.OptionalString(conflict, "remote", where))
        {
            MachineId = machineId,
            MachineName = machineName,
            DetectedAt = Timestamp(JsonMembers.RequireString(conflict, "detectedAt", where), where),
        };
    }

    private static Dictionary<SyncItemKey, string> ReadContents(JsonObject root, string name, string where)
    {
        var contents = new Dictionary<SyncItemKey, string>();
        if (root[name] is null)
        {
            return contents;
        }

        foreach (var (key, value) in JsonMembers.RequireObject(root[name], $"'{name}' of {where}"))
        {
            if (value is not JsonValue text || !text.TryGetValue(out string? content))
            {
                throw new ConfigFormatException($"'{name}' of {where} must map item keys to strings.");
            }

            contents[Key(key, where)] = content;
        }

        return contents;
    }

    private static void WriteContents(Utf8JsonWriter writer, string name, IReadOnlyDictionary<SyncItemKey, string> contents)
    {
        writer.WriteStartObject(name);
        foreach (var (key, content) in contents.Select(pair => (pair.Key.ToString(), pair.Value)).OrderBy(pair => pair.Item1, StringComparer.Ordinal))
        {
            writer.WriteString(key, content);
        }

        writer.WriteEndObject();
    }

    private static void WriteKeys(Utf8JsonWriter writer, string name, IEnumerable<string> keys)
    {
        writer.WriteStartArray(name);
        foreach (var key in keys.Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(key);
        }

        writer.WriteEndArray();
    }

    private static void WriteGuid(Utf8JsonWriter writer, string name, Guid? value)
    {
        if (value is { } guid)
        {
            writer.WriteString(name, guid);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private static SyncItemKey Key(string text, string where)
        => SyncItemKey.TryParse(text, out var key) ? key : throw new ConfigFormatException($"'{text}' in {where} is not a sync item key.");

    private static DateTimeOffset Timestamp(string text, string where)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var when)
            ? when
            : throw new ConfigFormatException($"'{text}' in {where} is not an ISO 8601 date and time.");

    private static string Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
