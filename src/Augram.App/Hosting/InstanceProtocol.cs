using System.Buffers;
using System.Text.Json;

namespace Augram.App.Hosting;

/// <summary>
/// The single-instance pipe's wire format. Augram 0.1 sends one byte (<see cref="LegacyShow"/>) and reads nothing, and its
/// listener is inbound only and shows its window on every connection. This version stays compatible both ways: a request
/// is the byte <see cref="Marker"/> and then one UTF-8 JSON line,
/// <c>{"request":"hello","reason":null,"channel":"Dev","version":"0.2.0","commit":"3f1c2ab","path":"…"}</c>, and every
/// answer is one JSON line with the listener's own identity, <c>{"channel":…,"version":…,"commit":…,"path":…}</c>.
/// A listener treats anything else (the old byte, a connection that closes or breaks before a full line, an unknown
/// verb) as a show request: the connection is the request. JSON escapes line breaks inside strings, so a path can hold
/// any character.
/// </summary>
internal static class InstanceProtocol
{
    public const byte LegacyShow = 1;
    public const byte Marker = 2;
    public const int MaxLineBytes = 16 * 1024;
    private const byte NewLine = (byte)'\n';

    public static byte[] EncodeRequest(InstanceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var buffer = new ArrayBufferWriter<byte>(256);
        buffer.Write([Marker]);
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("request", request.Kind switch
            {
                InstanceRequestKind.Hello => "hello",
                InstanceRequestKind.Quit => "quit",
                _ => "show",
            });
            json.WriteString("reason", request.Reason);
            WriteIdentity(json, request.From);
            json.WriteEndObject();
        }

        buffer.Write([NewLine]);
        return buffer.WrittenSpan.ToArray();
    }

    public static byte[] EncodeIdentity(InstanceIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            WriteIdentity(json, identity);
            json.WriteEndObject();
        }

        buffer.Write([NewLine]);
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>The request on one line (without the marker); null when it is not a JSON object with an identity. An unknown verb reads as Show.</summary>
    public static InstanceRequest? DecodeRequest(ReadOnlyMemory<byte> line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || ReadIdentity(root) is not { } from)
            {
                return null;
            }

            var kind = Text(root, "request") switch
            {
                "hello" => InstanceRequestKind.Hello,
                "quit" => InstanceRequestKind.Quit,
                _ => InstanceRequestKind.Show,
            };
            return new InstanceRequest(kind, from, Text(root, "reason"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>An answer line; null when it is not a JSON object with an identity.</summary>
    public static InstanceIdentity? DecodeIdentity(ReadOnlyMemory<byte> line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.ValueKind == JsonValueKind.Object ? ReadIdentity(document.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads up to the first line break; null when the stream ends first or the line exceeds <see cref="MaxLineBytes"/>.</summary>
    public static async Task<byte[]?> ReadLineAsync(Stream stream, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var buffer = new byte[MaxLineBytes];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count), cancellation).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            var end = Array.IndexOf(buffer, NewLine, count, read);
            count += read;
            if (end >= 0)
            {
                return buffer[..end];
            }
        }

        return null;
    }

    /// <summary>Reads one byte; -1 when the stream ended.</summary>
    public static async Task<int> ReadByteAsync(Stream stream, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var one = new byte[1];
        var read = await stream.ReadAsync(one.AsMemory(), cancellation).ConfigureAwait(false);
        return read == 0 ? -1 : one[0];
    }

    /// <summary>Reads until the other side closes, so an answer is taken before this side lets the pipe go.</summary>
    public static async Task WaitForCloseAsync(Stream stream, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var sink = new byte[64];
        while (await stream.ReadAsync(sink.AsMemory(), cancellation).ConfigureAwait(false) > 0)
        {
            // Nothing more is expected; drain until the other side closes.
        }
    }

    private static void WriteIdentity(Utf8JsonWriter json, InstanceIdentity identity)
    {
        json.WriteString("channel", identity.App.Channel.ToString());
        json.WriteString("version", identity.App.Version);
        json.WriteString("commit", identity.App.Commit);
        json.WriteString("path", identity.ExecutablePath);
    }

    private static InstanceIdentity? ReadIdentity(JsonElement root)
    {
        if (Text(root, "version") is not { Length: > 0 } version)
        {
            return null;
        }

        var app = new AppInfo(version, Text(root, "commit"), AppInfo.ParseChannel(Text(root, "channel")));
        return new InstanceIdentity(app, Text(root, "path") ?? string.Empty);
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
