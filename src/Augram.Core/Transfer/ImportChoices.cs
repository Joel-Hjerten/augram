using Augram.Core.Sync;

namespace Augram.Core.Transfer;

/// <summary>
/// The user's answers to an <see cref="ImportPlan"/> (plan 0003): a <see cref="SyncChoice"/> per differing item by its key
/// (<see cref="ImportPlan.Conflicts"/>), <see cref="Default"/> for the rest (Keep mine unless "apply to all" said otherwise),
/// and whether to take the file's options. Keys a plan does not have are passed over, so the same choices can be applied
/// to a plan made again after the stores moved.
/// </summary>
public sealed record ImportChoices
{
    /// <summary>Keep mine everywhere and leave the options alone: the preview.</summary>
    public static ImportChoices KeepMine { get; } = new();

    public IReadOnlyDictionary<SyncItemKey, SyncChoice> Items { get; init; } = new Dictionary<SyncItemKey, SyncChoice>();

    /// <summary>The choice for a differing item <see cref="Items"/> does not name.</summary>
    public SyncChoice Default { get; init; } = SyncChoice.KeepMine;

    /// <summary>Take the file's options (plan 0003, decision 2: off unless ticked).</summary>
    public bool TakeSettings { get; init; }

    public SyncChoice For(SyncItemKey key) => Items.TryGetValue(key, out var choice) ? choice : Default;
}
