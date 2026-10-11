using Augram.Core.Text;

namespace Augram.Core.Sync;

/// <summary>
/// The words for each <see cref="SyncItemKind"/>, one table for every line that names them: Options › Sync's "Last synced",
/// the import review and its summary, a conflict's kind, the export dialog's file contents and the sync log
/// (<see cref="SyncCounts"/>).
/// </summary>
public static class SyncItemNouns
{
    /// <summary>The kinds in the order a line names them: gestures, app groups, categories, hold remaps, commands, own steps, excluded apps.</summary>
    public static IReadOnlyList<SyncItemKind> ReadingOrder { get; } =
        [SyncItemKind.Gesture, SyncItemKind.Group, SyncItemKind.Category, SyncItemKind.HoldRemap, SyncItemKind.Command, SyncItemKind.CommandVersion, SyncItemKind.Ignored];

    /// <summary>"1 app group", "2 categories", "1 command's own steps", "3 excluded apps".</summary>
    public static string Count(SyncItemKind kind, int count)
    {
        var words = For(kind);
        return Sentence.Count(count, words.Singular, words.Plural);
    }

    /// <summary>"App group", "Own steps": the kind as a heading, before an item's name.</summary>
    public static string Heading(SyncItemKind kind) => For(kind).Heading;

    /// <summary>"gestures", "groups", "excluded apps": the plural the sync log uses.</summary>
    public static string LogPlural(SyncItemKind kind) => For(kind).Log;

    private static Words For(SyncItemKind kind) => kind switch
    {
        SyncItemKind.Gesture => new("gesture", "gestures", "Gesture", "gestures"),
        SyncItemKind.Group => new("app group", "app groups", "App group", "groups"),
        SyncItemKind.Category => new("category", "categories", "Category", "categories"),
        SyncItemKind.HoldRemap => new("hold remap", "hold remaps", "Hold remap", "hold remaps"),
        SyncItemKind.Command => new("command", "commands", "Command", "commands"),
        SyncItemKind.CommandVersion => new("command's own steps", "commands' own steps", "Own steps", "commands"),
        _ => new("excluded app", "excluded apps", "Excluded app", "excluded apps"),
    };

    private sealed record Words(string Singular, string Plural, string Heading, string Log);
}
