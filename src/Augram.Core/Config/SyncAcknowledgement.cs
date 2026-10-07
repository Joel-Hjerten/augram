namespace Augram.Core.Config;

/// <summary>
/// One entry of a sync file's <c>merged</c> list: "this machine has merged revision <see cref="Revision"/> of
/// machine <see cref="MachineId"/>'s file". It is what lets that machine know which of its own versions the
/// two now share (the merge base, <c>Sync/README.md</c>). <see cref="Except"/> are the item keys of that
/// revision this machine did not take (pending conflicts on either side); <see cref="Pending"/> are the keys
/// on which this machine has a conflict with that machine waiting for the user, which the other machine
/// leaves alone until it is resolved. Keys are the <c>SyncItemKey</c> text form ("command:&lt;id&gt;").
/// </summary>
public sealed record SyncAcknowledgement(
    Guid MachineId,
    Guid Revision,
    IReadOnlyList<string> Except,
    IReadOnlyList<string> Pending);
