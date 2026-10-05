using System.Text.Json;
using System.Text.Json.Serialization;

namespace Augram.Core.Config;

/// <summary>
/// Source-generated serializer metadata for <see cref="ConfigDocument"/>, so the app stays
/// trimming- and AOT-friendly. camelCase names, enums as strings (flags comma-separated),
/// comments and trailing commas tolerated on read because the file is meant to be hand-edited.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    Converters = [typeof(GestureIdJsonConverter), typeof(GestureSamplesJsonConverter), typeof(TrailSettingsJsonConverter)])]
[JsonSerializable(typeof(ConfigDocument))]
internal sealed partial class ConfigJsonContext : JsonSerializerContext
{
}
