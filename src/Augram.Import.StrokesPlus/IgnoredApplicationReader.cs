using System.Text.Json;
using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads <c>IgnoredApplications[]</c> into <see cref="IgnoredApp"/> records (plan 0001 §C1, F5):
/// description as the name (unique), <c>Active</c>, the matcher through <see cref="MatcherReader"/>
/// and <c>DisableOnFocus</c> as <see cref="IgnoredApp.DisableEntirely"/>. An entry whose matcher ends
/// up empty is imported inactive and reported, like an app group.
/// </summary>
internal sealed class IgnoredApplicationReader
{
    private const string FallbackName = "Ignored app";

    private readonly List<ImportWarning> _warnings;
    private readonly ImportedNames _names;

    public IgnoredApplicationReader(List<ImportWarning> warnings)
    {
        _warnings = warnings;
        _names = new ImportedNames("ignored app", warnings);
    }

    public IReadOnlyList<IgnoredApp> Read(JsonElement root)
    {
        var apps = new List<IgnoredApp>();
        if (!JsonRead.TryArray(root, StrokesPlusJson.IgnoredApplications, out var array))
        {
            return apps;
        }

        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            index++;
            if (element.ValueKind != JsonValueKind.Object)
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, StrokesPlusJson.IgnoredApplications + " #" + index, "Ignored application entry is not an object; skipped."));
                continue;
            }

            apps.Add(ReadApp(element, index));
        }

        return apps;
    }

    private IgnoredApp ReadApp(JsonElement application, int index)
    {
        var description = JsonRead.Text(application, StrokesPlusJson.Application.Description);
        var name = _names.Claim(description.Length == 0 ? FallbackName + " " + index : description);
        var matcher = MatcherReader.Read(application, name, _warnings);
        var isActive = JsonRead.Flag(application, StrokesPlusJson.Application.Active, whenAbsent: true);
        if (matcher.IsEmpty)
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, name, ApplicationReader.EmptyMatcherMessage));
            isActive = false;
        }

        var disableEntirely = JsonRead.Flag(application, StrokesPlusJson.Application.DisableOnFocus);
        return new IgnoredApp(GroupId.New(), name, isActive, matcher, disableEntirely);
    }
}
