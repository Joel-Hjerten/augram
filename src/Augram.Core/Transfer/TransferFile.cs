using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Transfer;

/// <summary>
/// An Augram JSON file as an export writes it and an import reads it (requirements F8, plan 0003): the config file's members,
/// each one optional. Never a second format: <see cref="TransferSerializer"/> writes the config file's envelope with the absent
/// members left out, and any Augram JSON (an export, <c>augram.json</c>, a backup, a sync machine file) reads as one.
/// Immutable.
/// </summary>
/// <param name="Gestures">The gestures in the file (an export of app groups carries the ones their commands use).</param>
/// <param name="Mapping">The mapping, validated; null when the file has none (a gestures-only export). A file's mapping always
/// has a Global group, the format requires one: an empty one is a shell that imports nothing.</param>
public sealed record TransferFile(IReadOnlyList<Gesture> Gestures, MappingDocument? Mapping)
{
    /// <summary>What an export's file name ends with: <c>.json</c> for every editor, <c>.augram</c> for whose it is.</summary>
    public const string Extension = ".augram.json";

    /// <summary>
    /// The options in the file, null when it has none (only "everything" carries them). Never this machine's sync settings:
    /// the writer leaves the <c>sync</c> member out and the reader drops it, so it is always <see cref="SyncSettings.Default"/> here.
    /// </summary>
    public Settings? Settings { get; init; }

    /// <summary>The schema version the file was written in; an older one was migrated on the way in.</summary>
    public int SchemaVersion { get; init; } = ConfigDocument.CurrentSchemaVersion;

    /// <summary>What reading noticed without refusing the file: steps kept as is (a type this build lacks), one line each.</summary>
    public IReadOnlyList<string> Notices { get; init; } = [];

    /// <summary>The mapping, or just the Global shell when the file has none.</summary>
    public MappingDocument MappingOrEmpty => Mapping ?? MappingDocument.Empty;
}
