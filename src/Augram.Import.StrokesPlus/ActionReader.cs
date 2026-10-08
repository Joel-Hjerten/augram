using System.Text.Json;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads an application's <c>Actions[]</c> into <see cref="Command"/> records (plan 0001 §C1): the
/// description as the name (unique within the group), the gesture name resolved to the id of the
/// gesture imported from the same file, a wheel flag as a wheel trigger, modifier and chord flags as
/// "inactive with a note" (deferred), steps through <see cref="StepReader"/>, a script-only action whose
/// script is one <c>sp.RunProgram</c> call as the step it makes (<see cref="ProgramCallMapping"/>), any
/// other script-only action as a placeholder step, and an action with neither as an override to nothing. A trigger bound twice in
/// one group keeps the active command bound (A7) and imports the other without it.
/// </summary>
internal sealed class ActionReader
{
    public const string ModifierNote = "Imported from StrokesPlus.net: needs modifier/rocker support (deferred)";
    public const string ScriptNote = "Imported from StrokesPlus.net: script-only action";
    private const string FallbackName = "Action";

    private readonly List<ImportWarning> _warnings;
    private readonly IReadOnlyDictionary<string, GestureId> _gestures;
    private readonly StepReader _steps;

    /// <summary><paramref name="gesturesByName"/> is source gesture name → imported id, compared case-insensitively.</summary>
    public ActionReader(List<ImportWarning> warnings, IReadOnlyDictionary<string, GestureId> gesturesByName, StepReader steps)
    {
        _warnings = warnings;
        _gestures = gesturesByName;
        _steps = steps;
    }

    /// <summary>
    /// The application's commands in file order, and beside each (same index) the trimmed <c>Category</c>
    /// name of its action, empty when it has none; <see cref="CategoryReader"/> turns those into categories.
    /// </summary>
    public (List<Command> Commands, List<string> Categories) ReadCommands(JsonElement application, string groupName)
    {
        var commands = new List<Command>();
        var categories = new List<string>();
        if (!JsonRead.TryArray(application, StrokesPlusJson.Actions, out var actions))
        {
            return (commands, categories);
        }

        var names = new HashSet<string>(MappingRules.NameComparer);
        var bound = new Dictionary<Trigger, int>();
        var index = 0;
        foreach (var element in actions.EnumerateArray())
        {
            index++;
            if (element.ValueKind != JsonValueKind.Object)
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, $"{groupName} #{index}", "Action entry is not an object; skipped."));
                continue;
            }

            commands.Add(Bound(ReadAction(element, index, names), element, groupName, commands, bound));
            categories.Add(JsonRead.Text(element, StrokesPlusJson.Action.Category));
        }

        return (commands, categories);
    }

    private Command ReadAction(JsonElement action, int index, HashSet<string> names)
    {
        var description = JsonRead.Text(action, StrokesPlusJson.Action.Description);
        var name = UniqueName(description.Length == 0 ? FallbackName + " " + index : description, names);
        var isActive = JsonRead.Flag(action, StrokesPlusJson.Action.Active, whenAbsent: true);
        var notes = new List<string>();
        if (StrokesPlusJson.Action.ModifierFlags.Any(flag => JsonRead.Flag(action, flag)))
        {
            isActive = false;
            notes.Add(ModifierNote);
        }

        var steps = _steps.ReadAll(action, name);
        var script = JsonRead.Text(action, StrokesPlusJson.Action.Script);
        if (steps.Count == 0 && script.Length > 0)
        {
            if (RunProgramScript.TryRecognize(script, out var call, out _))
            {
                steps.Add(new CommandStep(ProgramCallMapping.ToStep(call), HostPlatform.Windows));
            }
            else
            {
                steps.Add(_steps.Script(script, name));
                notes.Add(ScriptNote);
            }
        }

        var note = notes.Count == 0 ? null : string.Join(Environment.NewLine, notes);
        return new Command(CommandId.New(), name, ReadTrigger(action, name), isActive, steps, note);
    }

    private Trigger ReadTrigger(JsonElement action, string commandName)
    {
        var gestureName = JsonRead.Text(action, StrokesPlusJson.Action.GestureName);
        if (gestureName.Length > 0)
        {
            if (_gestures.TryGetValue(gestureName, out var id))
            {
                return Trigger.ForGesture(id);
            }

            _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, $"gesture '{gestureName}' not found; command imported without a gesture"));
            return Trigger.None;
        }

        var up = JsonRead.Flag(action, StrokesPlusJson.Action.WheelUp);
        var down = JsonRead.Flag(action, StrokesPlusJson.Action.WheelDown);
        if (up && down)
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, "Both wheel directions are set; imported as wheel up."));
        }

        if (up || down)
        {
            return Trigger.ForWheel(up ? WheelDirection.Up : WheelDirection.Down);
        }

        _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, "No gesture or wheel trigger; command imported without a trigger."));
        return Trigger.None;
    }

    /// <summary>A7: one command per trigger per group. When two actions share a trigger the active one stays bound.</summary>
    private Command Bound(Command command, JsonElement action, string groupName, List<Command> commands, Dictionary<Trigger, int> bound)
    {
        if (!command.Trigger.IsBound)
        {
            return command;
        }

        if (!bound.TryGetValue(command.Trigger, out var position))
        {
            bound[command.Trigger] = commands.Count;
            return command;
        }

        var phrase = TriggerPhrase(action);
        var other = commands[position];
        if (command.IsActive && !other.IsActive)
        {
            commands[position] = other with { Trigger = Trigger.None };
            bound[command.Trigger] = commands.Count;
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, other.Name, $"'{other.Name}' and '{command.Name}' in '{groupName}' both use {phrase}; the inactive '{other.Name}' is imported without it."));
            return command;
        }

        _warnings.Add(new ImportWarning(ImportSeverity.Warning, command.Name, $"'{other.Name}' already uses {phrase} in '{groupName}'; '{command.Name}' is imported without it."));
        return command with { Trigger = Trigger.None };
    }

    private static string TriggerPhrase(JsonElement action)
    {
        var gesture = JsonRead.Text(action, StrokesPlusJson.Action.GestureName);
        if (gesture.Length > 0)
        {
            return $"gesture '{gesture}'";
        }

        return JsonRead.Flag(action, StrokesPlusJson.Action.WheelUp) ? "wheel up" : "wheel down";
    }

    private string UniqueName(string name, HashSet<string> names)
    {
        if (names.Add(name))
        {
            return name;
        }

        var n = 2;
        var candidate = name + " (" + n + ")";
        while (!names.Add(candidate))
        {
            n++;
            candidate = name + " (" + n + ")";
        }

        _warnings.Add(new ImportWarning(ImportSeverity.Warning, name, "Duplicate command name; imported as '" + candidate + "'."));
        return candidate;
    }
}
