using System.Text.Json.Serialization;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

/// <summary>
/// The whole on-disk configuration (requirements F8): one JSON file, <c>schemaVersion</c>
/// first, settings, the gesture library, then the mapping. Export uses the same shape. Immutable;
/// the running app rebuilds it from the stores (<see cref="ConfigSession.Document"/>). The
/// constructor parameters are optional so a missing section takes its default on read.
/// </summary>
public sealed record ConfigDocument
{
    /// <summary>
    /// The version this build writes and the highest it can read. 1: everything up to 0.4.0. 2 (2026-10-09): trigger
    /// combinations (a trigger's <c>hold</c>, the click trigger, an own version's <c>trigger</c>); a version 1 build would
    /// read a combination as the plain trigger and save it back that way, or refuse a click trigger, so it must refuse the file.
    /// 3 (2026-10-09): the StrokesPlus.net app definition's matcher fields; a version 2 build would ignore them and match too
    /// many windows. 4 (2026-10-10): hold remaps (a group's <c>holdRemaps</c>, a command's <c>holdRemap</c>, the input trigger
    /// <c>{ "input": … }</c>); a version 3 build would read the commands under a hold remap as ordinary ones and save the file
    /// back without its hold remaps, or refuse the whole file at the first input, so it must refuse the file.
    /// </summary>
    public const int CurrentSchemaVersion = 4;

    public ConfigDocument(int schemaVersion = CurrentSchemaVersion, Settings? settings = null, IReadOnlyList<Gesture>? gestures = null)
    {
        SchemaVersion = schemaVersion;
        Settings = settings ?? Settings.Default;
        Gestures = gestures ?? [];
    }

    public int SchemaVersion { get; init; }

    public Settings Settings { get; init; }

    public IReadOnlyList<Gesture> Gestures { get; init; }

    /// <summary>
    /// App groups, commands and ignored apps (F5, F5a). Not handled by <see cref="ConfigJsonContext"/>:
    /// a step's parameters are whatever its type writes, so <see cref="ConfigSerializer"/> reads and
    /// writes this member through <c>MappingJsonReader</c> / <c>MappingJsonWriter</c>. Missing in the
    /// file means <see cref="MappingDocument.Empty"/>, which is why a file from before M2 still loads
    /// under schema version 1.
    /// </summary>
    [JsonIgnore]
    public MappingDocument Mapping { get; init; } = MappingDocument.Empty;

    /// <summary>A fresh install: default settings and the starter gestures (plan 0001 C3).</summary>
    public static ConfigDocument Default { get; } = new() { Gestures = StarterGestures.All() };
}
