using Augram.Core.Sync;

namespace Augram.Core.Transfer;

/// <summary>
/// One item of the file as the import sees it (plan 0003): its key (the local item's when it matched one), its name (the
/// local one when it is here, else the file's), its <see cref="ImportStatus"/>, and how it was matched when not by id.
/// A command's own steps are an entry of their own unless the command itself differs; then they follow its choice.
/// </summary>
public sealed record ImportEntry(SyncItemKey Key, string Name, ImportStatus Status)
{
    public SyncItemKind Kind => Key.Kind;

    /// <summary>Set when the item took a local id by name, hold key or shape; null when matched by id, or new.</summary>
    public ImportMatch? Match { get; init; }
}
