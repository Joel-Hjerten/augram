using System.Text.Json;
using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads the matcher fields an <c>Application</c> and an <c>IgnoredApplication</c> share into an
/// <see cref="AppMatcher"/> (plan 0001 §C1, F5), one SP.net field to one Augram field with its Use Regex flag
/// (Joel, 2026-10-09): <c>FileName</c> to the Windows executable names (a plain regex alternation of names becomes the
/// list, so the known-app guess still works on a Mac; any other pattern stays a pattern), <c>FilePath</c> to the path, the
/// owner's, root's, parent's and control's window text to the title (the root owner's) and the root, parent and control
/// titles, the four class names to the owner, root, parent and control classes, and <c>IgnoreFullScreen</c> to A21.
/// <c>ControlID</c> has no Augram equivalent and is reported. A regex the engine refuses leaves its field empty, with a
/// warning, rather than failing the group.
/// </summary>
internal static class MatcherReader
{
    public static AppMatcher Read(JsonElement application, string item, List<ImportWarning> warnings)
    {
        ReportControlId(application, item, warnings);
        MatcherField? Field(string name) => ReadValidField(application, name, item, warnings);

        var names = Field(StrokesPlusJson.Application.FileName);
        var path = Field(StrokesPlusJson.Application.FilePath);
        var title = Field(StrokesPlusJson.Application.OwnerWindowText);
        var rootTitle = Field(StrokesPlusJson.Application.RootWindowText);
        var parentTitle = Field(StrokesPlusJson.Application.ParentWindowText);
        var controlTitle = Field(StrokesPlusJson.Application.ControlWindowText);
        var ownerClass = Field(StrokesPlusJson.Application.OwnerClassName);
        var rootClass = Field(StrokesPlusJson.Application.RootClassName);
        var parentClass = Field(StrokesPlusJson.Application.ParentClassName);
        var controlClass = Field(StrokesPlusJson.Application.ControlClassName);
        var (processNames, namesAreRegex) = ProcessNames(names);
        return new AppMatcher
        {
            WindowsProcessNames = processNames,
            WindowsProcessNamesAreRegex = namesAreRegex,
            ProcessPath = path?.Value,
            ProcessPathIsRegex = path?.IsRegex ?? false,
            Title = title?.Value,
            TitleIsRegex = title?.IsRegex ?? false,
            RootTitle = rootTitle?.Value,
            RootTitleIsRegex = rootTitle?.IsRegex ?? false,
            ParentTitle = parentTitle?.Value,
            ParentTitleIsRegex = parentTitle?.IsRegex ?? false,
            ControlTitle = controlTitle?.Value,
            ControlTitleIsRegex = controlTitle?.IsRegex ?? false,
            OwnerClass = ownerClass?.Value,
            OwnerClassIsRegex = ownerClass?.IsRegex ?? false,
            RootClass = rootClass?.Value,
            RootClassIsRegex = rootClass?.IsRegex ?? false,
            ParentClass = parentClass?.Value,
            ParentClassIsRegex = parentClass?.IsRegex ?? false,
            ControlClass = controlClass?.Value,
            ControlClassIsRegex = controlClass?.IsRegex ?? false,
            IgnoreWhenFullScreen = JsonRead.Flag(application, StrokesPlusJson.Application.IgnoreFullScreen),
        };
    }

    /// <summary>A plain name, a plain list of names from an alternation (<c>^(chrome|msedge)\.exe$</c>), or the pattern itself.</summary>
    private static (IReadOnlyList<string> Names, bool AreRegex) ProcessNames(MatcherField? field)
    {
        if (field is null)
        {
            return ([], false);
        }

        if (!field.IsRegex)
        {
            return ([field.Value], false);
        }

        return RegexAlternation.TryReadLiterals(field.Value, out var names) ? (names, false) : ([field.Value], true);
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

    /// <summary>A field as <see cref="ReadField"/> reads it, or null with a warning when it is a regex the engine refuses.</summary>
    private static MatcherField? ReadValidField(JsonElement application, string name, string item, List<ImportWarning> warnings)
    {
        var field = ReadField(application, name);
        if (field is null || !field.IsRegex || MappingRules.PatternProblem(field.Value) is not { } problem)
        {
            return field;
        }

        warnings.Add(new ImportWarning(ImportSeverity.Warning, item, $"{name} pattern '{field.Value}' is not a valid regular expression: {problem} The field is left empty."));
        return null;
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
