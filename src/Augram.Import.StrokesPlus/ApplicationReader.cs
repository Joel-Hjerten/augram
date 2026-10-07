using System.Text.Json;
using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads <c>GlobalApplication</c> into the Global group's commands and each <c>Applications[]</c>
/// entry into an <see cref="AppGroup"/> (plan 0001 §C1, F5): description as the name (unique; "Global"
/// is reserved), <c>Active</c>, <c>NoGlobalActions</c> as <see cref="AppGroup.SuppressGlobals"/>, the
/// matcher through <see cref="MatcherReader"/> and the actions through <see cref="ActionReader"/>. A
/// group whose matcher ends up empty would match nothing, so it is imported inactive and reported.
/// </summary>
internal sealed class ApplicationReader
{
    public const string EmptyMatcherMessage = "No usable app definition; imported inactive (needs an app definition).";
    private const string FallbackName = "App";

    private readonly List<ImportWarning> _warnings;
    private readonly ActionReader _actions;
    private readonly HashSet<string> _names = new(MappingRules.NameComparer) { AppGroup.GlobalName };

    public ApplicationReader(List<ImportWarning> warnings, ActionReader actions)
    {
        _warnings = warnings;
        _actions = actions;
    }

    /// <summary>The Global group first, then the applications in file order.</summary>
    public IReadOnlyList<AppGroup> Read(JsonElement root)
    {
        var groups = new List<AppGroup> { ReadGlobal(root) };
        if (!JsonRead.TryArray(root, StrokesPlusJson.Applications, out var applications))
        {
            return groups;
        }

        var index = 0;
        foreach (var element in applications.EnumerateArray())
        {
            index++;
            if (element.ValueKind != JsonValueKind.Object)
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, StrokesPlusJson.Applications + " #" + index, "Application entry is not an object; skipped."));
                continue;
            }

            groups.Add(ReadApplication(element, index));
        }

        return groups;
    }

    private AppGroup ReadGlobal(JsonElement root)
    {
        if (!JsonRead.TryObject(root, StrokesPlusJson.GlobalApplication, out var global))
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Info, StrokesPlusJson.GlobalApplication, "No GlobalApplication found; the Global group imports empty."));
            return AppGroup.EmptyGlobal;
        }

        return AppGroup.EmptyGlobal with { Commands = _actions.ReadCommands(global, AppGroup.GlobalName) };
    }

    private AppGroup ReadApplication(JsonElement application, int index)
    {
        var description = JsonRead.Text(application, StrokesPlusJson.Application.Description);
        var name = UniqueName(description.Length == 0 ? FallbackName + " " + index : description);
        var matcher = MatcherReader.Read(application, name, _warnings);
        var isActive = JsonRead.Flag(application, StrokesPlusJson.Application.Active, whenAbsent: true);
        if (matcher.IsEmpty)
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, name, EmptyMatcherMessage));
            isActive = false;
        }

        var suppressGlobals = JsonRead.Flag(application, StrokesPlusJson.Application.NoGlobalActions);
        return new AppGroup(GroupId.New(), name, isActive, suppressGlobals, matcher, _actions.ReadCommands(application, name));
    }

    private string UniqueName(string name)
    {
        if (_names.Add(name))
        {
            return name;
        }

        var n = 2;
        var candidate = name + " (" + n + ")";
        while (!_names.Add(candidate))
        {
            n++;
            candidate = name + " (" + n + ")";
        }

        _warnings.Add(new ImportWarning(ImportSeverity.Warning, name, "Duplicate app name; imported as '" + candidate + "'."));
        return candidate;
    }
}
