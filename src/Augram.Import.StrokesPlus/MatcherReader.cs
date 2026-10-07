using System.Text.Json;
using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads the matcher fields an <c>Application</c> and an <c>IgnoredApplication</c> share into an
/// <see cref="AppMatcher"/> (plan 0001 §C1, F5): <c>FileName</c> to process names (a plain regex
/// alternation becomes the list), <c>FilePath</c> to the path with its regex flag, the window texts
/// to the title (Root first, then Owner, Parent, Control), the class names to the class chain, and
/// <c>IgnoreFullScreen</c> to A21. <c>ControlID</c> has no Augram equivalent and is reported. A regex
/// the engine refuses is dropped with a warning rather than failing the group.
/// </summary>
internal static class MatcherReader
{
    private static readonly string[] WindowTextFields =
    [
        StrokesPlusJson.Application.RootWindowText,
        StrokesPlusJson.Application.OwnerWindowText,
        StrokesPlusJson.Application.ParentWindowText,
        StrokesPlusJson.Application.ControlWindowText,
    ];

    private static readonly string[] ClassNameFields =
    [
        StrokesPlusJson.Application.OwnerClassName,
        StrokesPlusJson.Application.RootClassName,
        StrokesPlusJson.Application.ParentClassName,
        StrokesPlusJson.Application.ControlClassName,
    ];

    public static AppMatcher Read(JsonElement application, string item, List<ImportWarning> warnings)
    {
        var path = ReadField(application, StrokesPlusJson.Application.FilePath);
        var title = ReadTitle(application, item, warnings);
        ReportControlId(application, item, warnings);
        var matcher = new AppMatcher
        {
            ProcessNames = ReadProcessNames(application, item, warnings),
            ProcessPath = path?.Value,
            ProcessPathIsRegex = path?.IsRegex ?? false,
            Title = title?.Value,
            TitleIsRegex = title?.IsRegex ?? false,
            ClassChain = ReadClassChain(application, item, warnings),
            IgnoreWhenFullScreen = JsonRead.Flag(application, StrokesPlusJson.Application.IgnoreFullScreen),
        };
        return WithValidPatterns(matcher, item, warnings);
    }

    private static IReadOnlyList<string> ReadProcessNames(JsonElement application, string item, List<ImportWarning> warnings)
    {
        var field = ReadField(application, StrokesPlusJson.Application.FileName);
        if (field is null)
        {
            return [];
        }

        if (!field.IsRegex)
        {
            return [field.Value];
        }

        if (RegexAlternation.TryReadLiterals(field.Value, out var names))
        {
            return names;
        }

        warnings.Add(new ImportWarning(ImportSeverity.Warning, item, $"FileName pattern '{field.Value}' is not a plain list of names; the process name is left empty. Fill in the app definition by hand."));
        return [];
    }

    private static MatcherField? ReadTitle(JsonElement application, string item, List<ImportWarning> warnings)
    {
        MatcherField? chosen = null;
        string? chosenField = null;
        var differing = new List<string>();
        foreach (var name in WindowTextFields)
        {
            var field = ReadField(application, name);
            if (field is null)
            {
                continue;
            }

            if (chosen is null)
            {
                chosen = field;
                chosenField = name;
            }
            else if (field != chosen)
            {
                differing.Add(name);
            }
        }

        if (differing.Count > 0)
        {
            warnings.Add(new ImportWarning(ImportSeverity.Warning, item, $"{chosenField} is used as the title; {string.Join(", ", differing)} differ and are not imported."));
        }

        return chosen;
    }

    private static IReadOnlyList<string> ReadClassChain(JsonElement application, string item, List<ImportWarning> warnings)
    {
        var chain = new List<string>();
        foreach (var name in ClassNameFields)
        {
            var field = ReadField(application, name);
            if (field is null)
            {
                continue;
            }

            if (!field.IsRegex)
            {
                chain.Add(field.Value);
            }
            else if (RegexAlternation.TryReadLiterals(field.Value, out var alternatives))
            {
                chain.Add(string.Join('|', alternatives));
            }
            else
            {
                warnings.Add(new ImportWarning(ImportSeverity.Warning, item, $"{name} pattern '{field.Value}' is not a plain list of class names; skipped."));
            }
        }

        return chain;
    }

    private static void ReportControlId(JsonElement application, string item, List<ImportWarning> warnings)
    {
        var value = JsonRead.Member(application, StrokesPlusJson.Application.ControlId);
        var isSet = value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText() != "0",
            JsonValueKind.String => value.GetString()!.Trim().Length > 0,
            JsonValueKind.Object or JsonValueKind.Array => true,
            _ => false,
        };
        if (isSet)
        {
            warnings.Add(new ImportWarning(ImportSeverity.Warning, item, "ControlID has no Augram equivalent; skipped."));
        }
    }

    /// <summary>Drops a regex the engine refuses (path first, then title) so the group still imports; each drop is reported.</summary>
    private static AppMatcher WithValidPatterns(AppMatcher matcher, string item, List<ImportWarning> warnings)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                MappingRules.EnsureValid(matcher);
                return matcher;
            }
            catch (MappingValidationException exception)
            {
                warnings.Add(new ImportWarning(ImportSeverity.Warning, item, exception.Message + " The field is left empty."));
                matcher = matcher.ProcessPathIsRegex && matcher.ProcessPath is not null
                    ? matcher with { ProcessPath = null, ProcessPathIsRegex = false }
                    : matcher with { Title = null, TitleIsRegex = false };
            }
        }

        return matcher;
    }

    /// <summary>A <c>{ Value, IsRegex }</c> field; null when absent or its value is blank.</summary>
    private static MatcherField? ReadField(JsonElement application, string name)
    {
        if (!JsonRead.TryObject(application, name, out var field))
        {
            return null;
        }

        var value = JsonRead.Text(field, StrokesPlusJson.Matcher.Value);
        return value.Length == 0 ? null : new MatcherField(value, JsonRead.Flag(field, StrokesPlusJson.Matcher.IsRegex));
    }

    private sealed record MatcherField(string Value, bool IsRegex);
}
