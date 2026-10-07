using Augram.Core.Gestures;

namespace Augram.App.Components.SyncConflictList;

/// <summary>One machine's version of a conflicting item as a row cell shows it: a one-line summary and, for a gesture, the points its glyph draws.</summary>
public sealed record SyncConflictSide(string Text, IReadOnlyList<GesturePoint>? Points = null)
{
    public const string DeletedText = "deleted";

    /// <summary>The item was deleted on that machine.</summary>
    public static SyncConflictSide Deleted { get; } = new(DeletedText);

    public bool HasGlyph => Points is { Count: > 0 };
}
