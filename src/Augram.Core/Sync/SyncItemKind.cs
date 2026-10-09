namespace Augram.Core.Sync;

/// <summary>The kinds of item a gesture set and mapping split into for a merge (README: items).</summary>
public enum SyncItemKind
{
    /// <summary>One gesture, by <c>GestureId</c>.</summary>
    Gesture,

    /// <summary>An app group's header (name, active, suppress globals, matcher), by <c>GroupId</c>; its categories and commands are items of their own.</summary>
    Group,

    /// <summary>One category of one group, by group id and <c>CategoryId</c> (category ids are unique per group only).</summary>
    Category,

    /// <summary>One command with the id of its group, by <c>CommandId</c>; moving it to another group is a change.</summary>
    Command,

    /// <summary>One ignored app, by its id.</summary>
    Ignored,

    /// <summary>
    /// A command's own steps for the platform it was not authored on (F8), by the command's id: an item of its own so the
    /// original changing on one machine and the own steps changing on the other merge without a conflict.
    /// </summary>
    CommandVersion,

    /// <summary>
    /// One hold remap of one group (F9, sync format 11), by group id and <c>HoldRemapId</c> like a category: its header (name,
    /// hold key, tap time, active, Use on). The commands under it are command items carrying <c>holdRemap</c>.
    /// </summary>
    HoldRemap,
}
