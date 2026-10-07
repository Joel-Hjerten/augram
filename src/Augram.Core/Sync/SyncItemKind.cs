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
}
