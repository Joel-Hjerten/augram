namespace Augram.Core.Sync;

/// <summary>
/// Which choices a <see cref="SyncConflict"/> offers (README: conflicts), the one place the rule lives: Keep mine and Take
/// theirs always; Keep both for a gesture or a command present on both sides. The conflict dialog and the import review show
/// <see cref="Allowed"/>; resolving (the sync's and the import's) applies <see cref="Effective"/>, which turns a choice the
/// conflict does not offer into the nearest one, with a note saying so.
/// </summary>
public static class SyncChoiceRules
{
    private static readonly SyncChoice[] All = [SyncChoice.KeepMine, SyncChoice.TakeTheirs, SyncChoice.KeepBoth];
    private static readonly SyncChoice[] WithoutKeepBoth = [SyncChoice.KeepMine, SyncChoice.TakeTheirs];

    /// <summary>Keep mine and Take theirs always; Keep both for a gesture or a command present on both sides.</summary>
    public static IReadOnlyList<SyncChoice> Allowed(SyncConflict conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        return conflict.Kind is SyncItemKind.Gesture or SyncItemKind.Command && !conflict.DeletedHere && !conflict.DeletedThere
            ? All
            : WithoutKeepBoth;
    }

    /// <summary>The choice that will actually be applied, and the note to show when it differs from the one asked for.</summary>
    internal static (SyncChoice Choice, string? Note) Effective(SyncConflict conflict, SyncChoice choice)
    {
        if (choice != SyncChoice.KeepBoth || Allowed(conflict).Contains(SyncChoice.KeepBoth))
        {
            return (choice, null);
        }

        if (conflict.DeletedThere)
        {
            return (SyncChoice.KeepMine, $"'{conflict.Name}' was deleted on {conflict.MachineName}, so keeping both keeps this machine's.");
        }

        if (conflict.DeletedHere)
        {
            return (SyncChoice.TakeTheirs, $"'{conflict.Name}' was deleted here, so keeping both takes {conflict.MachineName}'s.");
        }

        return (SyncChoice.TakeTheirs, $"Keep both is for gestures and commands; '{conflict.Name}' took {conflict.MachineName}'s version.");
    }
}
