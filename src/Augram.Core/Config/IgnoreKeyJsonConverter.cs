using System.Text.Json;
using System.Text.Json.Serialization;
using Augram.Core.Abstractions;

namespace Augram.Core.Config;

/// <summary>
/// <see cref="GeneralSettings.IgnoreKey"/> in the file: the <see cref="KeyModifiers"/> it holds, written and read by the names
/// the file has always used, <c>"Control, Win"</c> (the setting had its own enum until plan 0007, whose fourth member was
/// <c>Win</c> where <see cref="KeyModifiers"/> says <c>Meta</c>; same bits). The names go through the same string enum
/// converter the rest of the settings use, so a file reads and writes byte for byte as before.
/// </summary>
internal sealed class IgnoreKeyJsonConverter : JsonConverter<KeyModifiers>
{
    private static readonly JsonConverter<FileName> Names = (JsonConverter<FileName>)new JsonStringEnumConverter<FileName>().CreateConverter(typeof(FileName), JsonSerializerOptions.Default);

    /// <summary>The names in the file; the bits are <see cref="KeyModifiers"/>'.</summary>
    [Flags]
    private enum FileName
    {
        None = (int)KeyModifiers.None,
        Control = (int)KeyModifiers.Control,
        Alt = (int)KeyModifiers.Alt,
        Shift = (int)KeyModifiers.Shift,
        Win = (int)KeyModifiers.Meta,
    }

    public override KeyModifiers Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => (KeyModifiers)(int)Names.Read(ref reader, typeof(FileName), options);

    public override void Write(Utf8JsonWriter writer, KeyModifiers value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        Names.Write(writer, (FileName)(int)value, options);
    }
}
