using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Imported;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Turns the placeholders an earlier import saved into the step types that exist now (plan 0001 §C1), so imported commands
/// start working without a re-import: <c>SendHotKey</c>/<c>SendVKey</c> through <see cref="HotkeyMapping"/>,
/// <c>SendKeys</c>/<c>SendString</c> through <see cref="TextMapping"/> (one placeholder can become several steps),
/// <c>Run</c> through <see cref="RunMapping"/>, and a script-only action's <c>Script</c> placeholder through
/// <see cref="ScriptMapping"/>, the same routing a fresh import uses (the "script-only action" note goes with the script).
/// A script of nothing but comments maps to no steps: the placeholder is removed and the command does nothing here, an
/// override to nothing. A placeholder that still does not map, and a SendKeys string with a part that does not, stays as
/// it is. Each replacement keeps the placeholder's platform and active flag. Pure and idempotent: the caller commits the
/// result through the mapping store, and an upgraded mapping has nothing left to upgrade.
/// </summary>
public static class PlaceholderUpgrade
{
    /// <summary>The mapping with every upgradable placeholder replaced; the same instance and no methods when there was none.</summary>
    public static PlaceholderUpgradeResult Upgrade(MappingDocument mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        var methods = new Dictionary<string, int>(StringComparer.Ordinal);
        var groups = new List<AppGroup>(mapping.Groups.Count);
        var changed = false;
        foreach (var group in mapping.Groups)
        {
            var commands = group.Commands.Select(command => UpgradeCommand(command, methods)).ToList();
            var groupChanged = commands.Where((command, index) => !ReferenceEquals(command, group.Commands[index])).Any();
            groups.Add(groupChanged ? group with { Commands = commands } : group);
            changed |= groupChanged;
        }

        return new PlaceholderUpgradeResult(changed ? mapping with { Groups = groups } : mapping, methods);
    }

    private static Command UpgradeCommand(Command command, Dictionary<string, int> methods)
    {
        List<CommandStep>? steps = null;
        var scriptReplaced = false;
        for (var index = 0; index < command.Steps.Count; index++)
        {
            var step = command.Steps[index];
            var replacement = step.Step is ImportedStep placeholder ? Replace(placeholder) : null;
            if (replacement is null)
            {
                steps?.Add(step);
                continue;
            }

            steps ??= [.. command.Steps.Take(index)];
            steps.AddRange(replacement.Select(real => new CommandStep(real, step.AuthoredOn, step.IsActive)));
            var method = ((ImportedStep)step.Step).SourceMethod;
            methods[method] = methods.GetValueOrDefault(method) + 1;
            scriptReplaced |= method == StrokesPlusJson.Method.Script;
        }

        if (steps is null)
        {
            return command;
        }

        return command with { Steps = steps, Note = scriptReplaced ? WithoutScriptNote(command.Note) : command.Note };
    }

    private static IReadOnlyList<IStep>? Replace(ImportedStep placeholder)
    {
        if (HotkeyMapping.TryUpgrade(placeholder) is { } hotkey)
        {
            return [hotkey];
        }

        if (TextMapping.TryUpgrade(placeholder) is { IsClean: true, Steps.Count: > 0 } text)
        {
            return text.Steps;
        }

        if (RunMapping.TryUpgrade(placeholder) is { } run)
        {
            return [run];
        }

        return placeholder.SourceMethod == StrokesPlusJson.Method.Script
            && placeholder.Parameters.TryGetValue(StrokesPlusJson.Method.ScriptParameter, out var script)
                ? ScriptMapping.TryMap(script)
                : null;
    }

    /// <summary>The note without the importer's "script-only action" line; null when nothing else was in it.</summary>
    private static string? WithoutScriptNote(string? note)
    {
        if (note is null)
        {
            return null;
        }

        var lines = note.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line != ActionReader.ScriptNote).ToList();
        return lines.All(string.IsNullOrWhiteSpace) ? null : string.Join(Environment.NewLine, lines);
    }
}
