using System.Globalization;
using System.Text;
using Augram.Core.Config;
using Augram.Core.Mapping;

namespace Augram.Core.Transfer;

/// <summary>
/// Builds what an export writes from the current configuration (plan 0003, decisions 1, 4, 13–15). Pure: the caller hands in
/// <see cref="ConfigSession.Document"/> and writes the result with <see cref="TransferSerializer"/>. "Everything" is the
/// options (never the sync section), every gesture and the whole mapping; "gestures only" the library alone; a selection is
/// its app groups whole (header, categories, hold remaps, commands with their own versions), its ignored apps, and the
/// gestures those commands' triggers name, original and own version, in library order. A selection without Global still
/// writes Global, as an empty shell, because every Augram file has one; an import ignores the shell. A selection also takes
/// the Ignored › Per command entries its commands' "Not in" names (plan 0004), selected or not: the file is validated like
/// any mapping, and without them it would drop those ticks.
/// </summary>
public static class Exporter
{
    private const string InvalidFileNameCharacters = "\\/:*?\"<>|";

    public static TransferFile Export(ExportScope scope, ConfigDocument current)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(current);
        return scope switch
        {
            ExportScope.GesturesScope => new TransferFile(current.Gestures, Mapping: null),
            ExportScope.Selection selection => Selected(selection, current),
            _ => new TransferFile(current.Gestures, current.Mapping) { Settings = current.Settings with { Sync = SyncSettings.Default } },
        };
    }

    /// <summary>
    /// "Augram everything 2026-10-10.augram.json", "Augram gestures …", "Augram Blender …" for one group or ignored app,
    /// "Augram 3 groups …" for several groups, "Augram selection …" for a mix; characters a file name cannot hold become "-".
    /// </summary>
    public static string SuggestedFileName(ExportScope scope, MappingDocument mapping, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(mapping);
        var what = scope switch
        {
            ExportScope.GesturesScope => "gestures",
            ExportScope.Selection selection => Describe(selection, mapping),
            _ => "everything",
        };
        return $"Augram {SafeForFileName(what)} {date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}{TransferFile.Extension}";
    }

    private static TransferFile Selected(ExportScope.Selection selection, ConfigDocument current)
    {
        var mapping = current.Mapping;
        var groups = mapping.Groups.Where(group => selection.Groups.Contains(group.Id)).ToList();
        if (!groups.Any(group => group.IsGlobal))
        {
            var global = mapping.Groups.FirstOrDefault(group => group.IsGlobal) ?? AppGroup.EmptyGlobal;
            groups.Insert(0, global with { Commands = [], Categories = [], HoldRemaps = [] });
        }

        var notIn = groups.SelectMany(group => group.Commands).SelectMany(command => command.NotIn).ToHashSet();
        var ignored = mapping.Ignored.Where(app => selection.Ignored.Contains(app.Id) || (app.IsPerCommand && notIn.Contains(app.Id))).ToArray();
        var used = groups.SelectMany(group => group.Commands).SelectMany(command => command.GestureIds()).ToHashSet();
        var gestures = current.Gestures.Where(gesture => used.Contains(gesture.Id)).ToArray();
        return new TransferFile(gestures, MappingRules.ValidDocument(new MappingDocument(groups, ignored)));
    }

    private static string Describe(ExportScope.Selection selection, MappingDocument mapping)
    {
        var groups = mapping.Groups.Where(group => selection.Groups.Contains(group.Id)).Select(group => group.Name).ToArray();
        var ignored = mapping.Ignored.Where(app => selection.Ignored.Contains(app.Id)).Select(app => app.Name).ToArray();
        return (groups.Length, ignored.Length) switch
        {
            (1, 0) => groups[0],
            (0, 1) => ignored[0],
            ( > 1, 0) => string.Create(CultureInfo.InvariantCulture, $"{groups.Length} groups"),
            _ => "selection",
        };
    }

    private static string SafeForFileName(string text)
    {
        var safe = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            safe.Append(char.IsControl(c) || InvalidFileNameCharacters.Contains(c, StringComparison.Ordinal) ? '-' : c);
        }

        var trimmed = safe.ToString().Trim().Trim('.');
        return trimmed.Length == 0 ? "export" : trimmed;
    }
}
