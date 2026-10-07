using System.Diagnostics.CodeAnalysis;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Sync;

/// <summary>
/// The identity of a <see cref="SyncItem"/>: its kind and id, plus the group id for a category (category
/// ids are unique within their group only). Text form, used in the sync file and the state files:
/// <c>gesture:&lt;id&gt;</c>, <c>group:&lt;id&gt;</c>, <c>category:&lt;groupId&gt;/&lt;id&gt;</c>, <c>command:&lt;id&gt;</c>,
/// <c>ignored:&lt;id&gt;</c>, <c>version:&lt;commandId&gt;</c>.
/// </summary>
public readonly record struct SyncItemKey(SyncItemKind Kind, Guid Id, Guid Group = default)
{
    public static SyncItemKey ForGesture(GestureId id) => new(SyncItemKind.Gesture, id.Value);

    public static SyncItemKey ForGroup(GroupId id) => new(SyncItemKind.Group, id.Value);

    public static SyncItemKey ForCategory(GroupId groupId, CategoryId id) => new(SyncItemKind.Category, id.Value, groupId.Value);

    public static SyncItemKey ForCommand(CommandId id) => new(SyncItemKind.Command, id.Value);

    public static SyncItemKey ForIgnored(GroupId id) => new(SyncItemKind.Ignored, id.Value);

    public static SyncItemKey ForCommandVersion(CommandId id) => new(SyncItemKind.CommandVersion, id.Value);

    public override string ToString() => Kind == SyncItemKind.Category
        ? $"{Prefix(Kind)}:{Group:D}/{Id:D}"
        : $"{Prefix(Kind)}:{Id:D}";

    public static bool TryParse([NotNullWhen(true)] string? text, out SyncItemKey key)
    {
        key = default;
        int colon = text?.IndexOf(':', StringComparison.Ordinal) ?? -1;
        if (text is null || colon < 0)
        {
            return false;
        }

        var prefix = text[..colon];
        var rest = text[(colon + 1)..];
        foreach (var kind in Enum.GetValues<SyncItemKind>())
        {
            if (!string.Equals(prefix, Prefix(kind), StringComparison.Ordinal))
            {
                continue;
            }

            if (kind != SyncItemKind.Category)
            {
                bool parsed = Guid.TryParse(rest, out var id);
                key = new SyncItemKey(kind, id);
                return parsed;
            }

            int slash = rest.IndexOf('/', StringComparison.Ordinal);
            if (slash > 0 && Guid.TryParse(rest[..slash], out var group) && Guid.TryParse(rest[(slash + 1)..], out var category))
            {
                key = new SyncItemKey(kind, category, group);
                return true;
            }

            return false;
        }

        return false;
    }

    /// <exception cref="FormatException">Not a key's text form.</exception>
    public static SyncItemKey Parse(string text)
        => TryParse(text, out var key) ? key : throw new FormatException($"'{text}' is not a sync item key.");

    private static string Prefix(SyncItemKind kind) => kind switch
    {
        SyncItemKind.Gesture => "gesture",
        SyncItemKind.Group => "group",
        SyncItemKind.Category => "category",
        SyncItemKind.Command => "command",
        SyncItemKind.CommandVersion => "version",
        _ => "ignored",
    };
}
