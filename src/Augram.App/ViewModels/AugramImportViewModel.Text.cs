using System.Globalization;
using Augram.Core.Sync;
using Augram.Core.Transfer;

namespace Augram.App.ViewModels;

/// <summary>The words of the import review (plan 0003, decision 16): the counts per status and kind, the matched items, and the summary after the import.</summary>
public sealed partial class AugramImportViewModel
{
    /// <summary>The matched items listed one per line before "…and 9 more.".</summary>
    public const int MatchLinesShown = 12;

    /// <summary>Kinds in the order the review names them.</summary>
    private static readonly SyncItemKind[] KindOrder =
        [SyncItemKind.Gesture, SyncItemKind.Group, SyncItemKind.Category, SyncItemKind.HoldRemap, SyncItemKind.Command, SyncItemKind.CommandVersion, SyncItemKind.Ignored];

    /// <summary>"New: 1 app group, 8 commands · Same as yours: 2 gestures · Different: 1 command"; the statuses that have entries.</summary>
    public static string Counts(IReadOnlyList<ImportEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var parts = new List<string>(3);
        AddStatus(parts, "New", entries, ImportStatus.New);
        AddStatus(parts, "Same as yours", entries, ImportStatus.Same);
        AddStatus(parts, "Different", entries, ImportStatus.Different);
        return parts.Count == 0 ? "Nothing in the file." : string.Join(" · ", parts);
    }

    /// <summary>
    /// "Gesture 'North' in the file has the shape of your 'Up' (96).", "App group 'Chrome' in the file is your 'Chrome'.",
    /// "Hold remap 'Space' in the file is your 'Space': the same hold key."; at most <see cref="MatchLinesShown"/> lines.
    /// </summary>
    public static string Matches(IReadOnlyList<ImportEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var lines = entries.Where(entry => entry.Match is not null).Select(MatchLine).ToList();
        if (lines.Count <= MatchLinesShown)
        {
            return string.Join(Environment.NewLine, lines);
        }

        var more = string.Create(CultureInfo.InvariantCulture, $"…and {lines.Count - MatchLinesShown} more.");
        return string.Join(Environment.NewLine, lines.Take(MatchLinesShown).Append(more));
    }

    /// <summary>"Imported from Blender.augram.json: 1 app group, 1 hold remap and 8 commands added; 1 command changed; options taken."</summary>
    public static string Summary(string fileName, ImportResult result)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(result);
        var parts = new List<string>(3);
        if (Changes(result.Counts, counts => counts.Added) is { Length: > 0 } added)
        {
            parts.Add(added + " added");
        }

        if (Changes(result.Counts, counts => counts.Changed) is { Length: > 0 } changed)
        {
            parts.Add(changed + " changed");
        }

        if (result.SettingsChanged)
        {
            parts.Add("options taken");
        }

        return parts.Count == 0
            ? $"Nothing changed from {fileName}: you kept your version of every item."
            : $"Imported from {fileName}: {string.Join("; ", parts)}.";
    }

    private static void AddStatus(List<string> parts, string label, IReadOnlyList<ImportEntry> entries, ImportStatus status)
    {
        var kinds = KindOrder
            .Select(kind => (Kind: kind, Count: entries.Count(entry => entry.Status == status && entry.Kind == kind)))
            .Where(pair => pair.Count > 0)
            .Select(pair => Noun(pair.Kind, pair.Count))
            .ToList();
        if (kinds.Count > 0)
        {
            parts.Add($"{label}: {string.Join(", ", kinds)}");
        }
    }

    private static string MatchLine(ImportEntry entry) => entry.Match!.By switch
    {
        ImportMatchKind.Shape => string.Create(CultureInfo.InvariantCulture, $"Gesture '{entry.Match.FileName}' in the file has the shape of your '{entry.Name}' ({entry.Match.Score ?? 0:0})."),
        ImportMatchKind.HoldKey => $"Hold remap '{entry.Match.FileName}' in the file is your '{entry.Name}': the same hold key.",
        _ => $"{SyncConflictsViewModel.KindLabel(entry.Kind)} '{entry.Match.FileName}' in the file is your '{entry.Name}'.",
    };

    /// <summary>"1 gesture, 2 app groups and 8 commands" for the kinds whose picked count is not zero; empty when none.</summary>
    private static string Changes(SyncCounts counts, Func<SyncKindCounts, int> pick)
    {
        var parts = KindOrder
            .Where(kind => kind != SyncItemKind.CommandVersion && pick(counts.For(kind)) > 0)
            .Select(kind => Noun(kind, pick(counts.For(kind))))
            .ToList();
        return parts.Count switch
        {
            0 => string.Empty,
            1 => parts[0],
            _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
        };
    }

    private static string Noun(SyncItemKind kind, int count) => kind switch
    {
        SyncItemKind.Gesture => Count(count, "gesture", "gestures"),
        SyncItemKind.Group => Count(count, "app group", "app groups"),
        SyncItemKind.Category => Count(count, "category", "categories"),
        SyncItemKind.HoldRemap => Count(count, "hold remap", "hold remaps"),
        SyncItemKind.Command => Count(count, "command", "commands"),
        SyncItemKind.CommandVersion => Count(count, "command's own steps", "commands' own steps"),
        _ => Count(count, "ignored app", "ignored apps"),
    };

    private static string Count(int count, string singular, string plural)
        => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? singular : plural)}");
}
