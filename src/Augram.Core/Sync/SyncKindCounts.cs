namespace Augram.Core.Sync;

/// <summary>How many items of one kind a sync added, changed and deleted on this machine.</summary>
public sealed record SyncKindCounts(int Added, int Changed, int Deleted)
{
    public static SyncKindCounts None { get; } = new(0, 0, 0);

    public int Total => Added + Changed + Deleted;

    public SyncKindCounts Plus(SyncKindCounts other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(Added + other.Added, Changed + other.Changed, Deleted + other.Deleted);
    }

    /// <summary>"+1 ~2 -0", for the log.</summary>
    public override string ToString() => $"+{Added} ~{Changed} -{Deleted}";
}
