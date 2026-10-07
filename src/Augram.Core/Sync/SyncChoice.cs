namespace Augram.Core.Sync;

/// <summary>How the user resolves one <see cref="SyncConflict"/> (F8 sync).</summary>
public enum SyncChoice
{
    /// <summary>This machine's version stays and goes to the other machine on the next sync.</summary>
    KeepMine,

    /// <summary>The other machine's version replaces this one (a deletion deletes it here).</summary>
    TakeTheirs,

    /// <summary>Gestures and commands: mine stays and theirs is added beside it with a new id (renamed on a clash, unbound if its trigger is taken). Other kinds: take theirs.</summary>
    KeepBoth,
}
