using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Config;

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
    /// The sync format this build writes and the newest it reads (README: format version), separate from the config
    /// schema. Raise it with every change to what a sync item holds or to which item kinds exist: a build that reads a
    /// newer file pauses sync instead of merging what it cannot see. 1: every file before 2026-10-07 (no
    /// <c>formatVersion</c> member). 2: F8 cross-platform commands (Use on, macOS executable names, a command's own
    /// steps as an item of their own). 3 (2026-10-08): "Use on" on a category (a category item's <c>useOn</c>); a format 2
    /// build would read such a category as used everywhere and publish it back that way. 4 (2026-10-08): the step types
    /// TypeText, Run and Display (<c>typeText</c>, <c>run</c>, <c>displayMode</c>, <c>hdr</c>); a format 3 build cannot read
    /// a command holding one and would drop it. 5 (2026-10-09): Display mode's "highest available" refresh
    /// (<c>"refreshHz": "highest"</c>), which a format 4 build refuses to read. 6 (2026-10-09): trigger combinations (a
    /// trigger's <c>hold</c>, the click trigger, an own-steps item's <c>trigger</c>); a format 5 build would read a
    /// combination as the plain trigger and publish it back that way (the config schema went to 2 at the same time). 7
    /// (2026-10-09): the step types Scroll and Clear clipboard (<c>scroll</c>, <c>clearClipboard</c>); a format 6 build
    /// cannot read a command holding one and would drop the step. 8 (2026-10-09): the StrokesPlus.net app definition's
    /// fields (regex executable names; a macOS executable path and window title; root, parent and control titles; owner, root, parent and control classes); a format 7
    /// build would ignore them, match too many windows and publish the matcher back without them (the config schema went to 3).
    /// 9 (2026-10-09): the Open app step (<c>openApp</c>); a format 8 build keeps it as is but cannot run it. 10
    /// (2026-10-09): a gesture's <c>originalSamples</c> (shape cleanup); a format 9 build would drop them and publish the
    /// gesture without its original. 11 (2026-10-10): hold remaps (F9: the <c>holdRemap</c> item kind, a command item's
    /// <c>holdRemap</c>, the input trigger, the Remap step); a format 10 build would drop the hold remaps and publish the group
    /// without them, could not read an input trigger and would keep a Remap step only as is (the config schema went to 4).
    /// 12 (2026-10-10, plan 0004): a command's own drag distance (its trigger hold's <c>dragDistancePx</c>, in a command item
    /// and an own-steps item's trigger) and a Global command's <c>notIn</c>; a format 11 build would drop both and publish the
    /// commands without their drag distance and "Not in" (the config schema went to 5).
    /// 13 (2026-10-10, plan 0004 revised): Ignored › Per command entries (an ignored app item's <c>"scope": "PerCommand"</c>), and
    /// a command item's <c>notIn</c> names them instead of app groups; a format 12 build would read a Per command entry as an
    /// ignore of the whole app and switch Augram off over it, and would drop every "Not in" naming one (the config schema went to 6).
    /// 14 (2026-10-10, plan 0005): button triggers (<c>{ "button": "Left", "hold": … }</c>, in a command item and an own-steps
    /// item's trigger) and a command item's <c>alsoIn</c> (Exclusions › Global entries); a format 13 build could not read a file
    /// holding a button trigger and would skip it, and would drop every "Also in" and publish the commands without it (the
    /// config schema went to 7).
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
