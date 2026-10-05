using System.Text;
using System.Text.Json;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// A parsed StrokesPlus.net settings file. Tolerant on purpose: the UTF-8 BOM SP.net writes is
/// skipped, comments and trailing commas are allowed, and unknown members are ignored by the
/// readers. Only malformed JSON or a non-object root is an <see cref="ImportFormatException"/>.
/// </summary>
public sealed class StrokesPlusDocument : IDisposable
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly JsonDocument _document;

    private StrokesPlusDocument(JsonDocument document)
    {
        _document = document;
    }

    /// <summary>The top-level settings object.</summary>
    public JsonElement Root => _document.RootElement;

    public static StrokesPlusDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json.TrimStart('﻿'), Options);
        }
        catch (JsonException e)
        {
            throw new ImportFormatException("The file is not valid JSON: " + e.Message, e);
        }

        var kind = document.RootElement.ValueKind;
        if (kind != JsonValueKind.Object)
        {
            document.Dispose();
            throw new ImportFormatException("Expected a JSON object at the top level, found " + kind + ".");
        }

        return new StrokesPlusDocument(document);
    }

    public static StrokesPlusDocument Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Parse(reader.ReadToEnd());
    }

    public void Dispose() => _document.Dispose();
}
