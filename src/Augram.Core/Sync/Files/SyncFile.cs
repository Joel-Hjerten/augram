using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Sync.Files;

/// <summary>
/// One machine's file in the sync repo, <c>machines/&lt;machineId&gt;.json</c> (F8 sync): the config file's
/// <c>gestures</c> and <c>mapping</c>, written and read by the same code, under a machine header. Settings
/// never travel. <see cref="Revision"/> is new on every publish and <see cref="Merged"/> says which revision
/// of each other machine's file this one has merged (<see cref="SyncAcknowledgement"/>). Read and written by
/// <see cref="SyncFileSerializer"/>; a file read from the repo is already validated and normalised.
/// </summary>
public sealed record SyncFile(
    int SchemaVersion,
    Guid MachineId,
    string MachineName,
    DateTimeOffset WrittenAt,
    IReadOnlyList<Gesture> Gestures,
    MappingDocument Mapping)
{
    /// <summary>
    /// The sync format this build writes and the newest it reads, separate from the config schema. Raise it with every
    /// change to what a sync item holds or to which item kinds exist, and add a row to the history table in
    /// <c>Sync/README.md</c> (Format version): a build that reads a newer file pauses sync instead of merging what it cannot see.
    /// </summary>
    public const int CurrentFormatVersion = 14;

    /// <summary>The sync format the file was written in; 1 for a file without the member.</summary>
    public int FormatVersion { get; init; } = CurrentFormatVersion;

    /// <summary>Identifies this version of the file; a new Guid per publish.</summary>
    public Guid Revision { get; init; }

    /// <summary>The other machines' files this one has merged, one entry per machine.</summary>
    public IReadOnlyList<SyncAcknowledgement> Merged { get; init; } = [];

    /// <summary>This file's entry for <paramref name="machineId"/>, or null when it never merged that machine.</summary>
    public SyncAcknowledgement? MergedFrom(Guid machineId) => Merged.FirstOrDefault(entry => entry.MachineId == machineId);
}
