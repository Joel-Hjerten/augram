using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps;

namespace Augram.Core.Transfer;

/// <summary>
/// Text in, <see cref="TransferFile"/> out, and back (plan 0003). The members go through exactly the config file's code
/// (<see cref="ConfigJsonContext"/>, <c>MappingJsonWriter</c>, <see cref="ConfigSerializer"/>'s reader with its version check
/// and migrations), so an export never drifts from the config file; only the envelope is written here, as
/// <see cref="SyncFileSerializer"/> writes its own. A member the file lacks is left out (<c>settings</c> unless the export is
/// "everything", <c>mapping</c> for gestures only); <c>settings</c> is written and read <b>without its <c>sync</c> member</b>,
/// which names this machine and its repository, <b>and without its <c>appearance</c></b>, this machine's look (plan 0006). Reading refuses a newer schema, migrates an older one, and refuses a file
/// that breaks a gesture, mapping or settings rule (only a hand edit can), each as one <see cref="ConfigFormatException"/> line.
/// </summary>
public static class TransferSerializer
{
    private const string What = "The file";
    private const string SyncMember = "sync";
    private const string AppearanceMember = "appearance";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    public static string Write(TransferFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ConfigDocument.CurrentSchemaVersion);
            if (file.Settings is { } settings)
            {
                writer.WritePropertyName("settings");
                WithoutLocalSections(settings).WriteTo(writer);
            }

            writer.WritePropertyName("gestures");
            JsonSerializer.Serialize(writer, file.Gestures, ConfigJsonContext.Default.IReadOnlyListGesture);
            if (file.Mapping is { } mapping)
            {
                writer.WritePropertyName("mapping");
                MappingJsonWriter.Write(writer, mapping);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Reads, migrates and validates; every problem is the returned <paramref name="error"/> line, nothing throws.</summary>
    /// <param name="json">The file's text.</param>
    /// <param name="steps">The step types the mapping may use; a step of another type is kept as is, with a notice.</param>
    /// <param name="file">The file, when it could be read.</param>
    /// <param name="error">Why it could not, one line.</param>
    public static bool TryRead(string json, StepRegistry steps, [NotNullWhen(true)] out TransferFile? file, [NotNullWhen(false)] out string? error)
    {
        try
        {
            file = Read(json, steps);
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

    /// <exception cref="ConfigFormatException">Not JSON, not an Augram file, written by a newer Augram, or breaking a rule.</exception>
    public static TransferFile Read(string json, StepRegistry steps)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(steps);

        var root = ConfigSerializer.ParseObject(json, What);
        int written = ConfigSerializer.ReadSchemaVersion(root, What);
        var notices = new List<string>();
        var document = ConfigSerializer.Read(root, steps, notices.Add, What);

        // After Read the tree is the current schema (migrated in place), so these are the current members.
        bool hasSettings = root["settings"] is JsonObject;
        bool hasMapping = root["mapping"] is not null;
        var settings = TransferFile.Portable(document.Settings);
        try
        {
            if (hasSettings)
            {
                SettingsRules.EnsureValid(settings);
            }

            return new TransferFile(GestureRules.ValidSet(document.Gestures), hasMapping ? MappingRules.ValidDocument(document.Mapping) : null)
            {
                Settings = hasSettings ? settings : null,
                SchemaVersion = written,
                Notices = notices,
            };
        }
        catch (Exception ex) when (ex is GestureValidationException or MappingValidationException or SettingsValidationException)
        {
            throw new ConfigFormatException($"{What} breaks a rule: {ex.Message}", ex);
        }
    }

    /// <summary>The options as the config file writes them, minus <c>sync</c> and <c>appearance</c>.</summary>
    private static JsonObject WithoutLocalSections(Settings settings)
    {
        var node = JsonSerializer.SerializeToNode(settings, ConfigJsonContext.Default.Settings)!.AsObject();
        node.Remove(SyncMember);
        node.Remove(AppearanceMember);
        return node;
    }
}
