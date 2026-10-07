namespace Augram.Core.Sync;

/// <summary>What a merge had to change so the result passes the gesture and mapping rules (README: repairs).</summary>
public enum SyncRepairKind
{
    /// <summary>A name clash: the incoming gesture, group, category or command took a " (2)" suffix.</summary>
    Renamed,

    /// <summary>A7: the incoming command's trigger is already bound in its group, so it is now unbound.</summary>
    Unbound,

    /// <summary>The command's app group is gone, so it moved into Global.</summary>
    MovedToGlobal,

    /// <summary>The command's gesture is gone, so it is now unbound.</summary>
    TriggerCleared,

    /// <summary>The command's category is gone, so it is now Uncategorized.</summary>
    CategoryCleared,

    /// <summary>The category's app group is gone, so the category was dropped.</summary>
    CategoryDropped,
}
