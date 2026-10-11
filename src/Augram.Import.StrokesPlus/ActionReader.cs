using System.Text.Json;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads an application's <c>Actions[]</c> into <see cref="Command"/> records (plan 0001 §C1): the
/// description as the name (unique within the group), the trigger through <see cref="TriggerReader"/> (the gesture by name,
/// a wheel flag, a click, each with the keys and buttons held and the capture mode; learnings 0003 §3.8), steps through
/// <see cref="StepReader"/>, a script-only action whose script Augram can express as the steps it makes
/// (<see cref="ScriptMapping"/>: one <c>sp.RunProgram</c> or <c>sp.SendKeys</c> call, <c>clip.Clear()</c>, SP.net's snap and
/// Ctrl + wheel samples, or nothing but comments, which is no steps), any other script-only action as a placeholder step,
/// and an action with neither as an override to nothing. A trigger bound twice in one group (overlapping, as A7 has it) keeps the active command bound and
/// imports the other without it.
/// </summary>
internal sealed class ActionReader
{
    public const string ScriptNote = "Imported from StrokesPlus.net: script-only action";
    private const string FallbackName = "Action";

    private readonly List<ImportWarning> _warnings;
    private readonly StepReader _steps;
    private readonly TriggerReader _triggers;

    /// <summary><paramref name="triggers"/> resolves gesture names and the secondary stroke button of the same file.</summary>
    public ActionReader(List<ImportWarning> warnings, TriggerReader triggers, StepReader steps)
    {
        _warnings = warnings;
        _triggers = triggers;
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

        var names = new ImportedNames("command", _warnings);
        var index = 0;
        foreach (var element in actions.EnumerateArray())
        {
            index++;
            if (element.ValueKind != JsonValueKind.Object)
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, $"{groupName} #{index}", "Action entry is not an object; skipped."));
                continue;
            }

            commands.Add(Bound(ReadAction(element, index, names), element, groupName, commands));
            categories.Add(JsonRead.Text(element, StrokesPlusJson.Action.Category));
        }

        return (commands, categories);
    }

    private Command ReadAction(JsonElement action, int index, ImportedNames names)
    {
        var description = JsonRead.Text(action, StrokesPlusJson.Action.Description);
        var name = names.Claim(description.Length == 0 ? FallbackName + " " + index : description);
        var isActive = JsonRead.Flag(action, StrokesPlusJson.Action.Active, whenAbsent: true);
        var notes = new List<string>();

        var steps = _steps.ReadAll(action, name);
        var script = JsonRead.Text(action, StrokesPlusJson.Action.Script);
        if (steps.Count == 0 && script.Length > 0)
        {
            if (ScriptMapping.TryMap(script) is { } mapped)
            {
                steps.AddRange(mapped.Select(step => new CommandStep(step, HostPlatform.Windows)));
            }
            else
            {
                steps.Add(_steps.Script(script, name));
                notes.Add(ScriptNote);
            }
        }

        var (trigger, inactiveNote) = _triggers.Read(action, name, steps);
        if (inactiveNote is not null)
        {
            isActive = false;
            notes.Insert(0, inactiveNote);
        }

        var note = notes.Count == 0 ? null : string.Join(Environment.NewLine, notes);
        return new Command(CommandId.New(), name, trigger, isActive, steps, note);
    }

    /// <summary>A7: one command per trigger per group. When two actions' triggers overlap the active one stays bound.</summary>
    private Command Bound(Command command, JsonElement action, string groupName, List<Command> commands)
    {
        if (!command.Trigger.IsBound)
        {
            return command;
        }

        var position = commands.FindIndex(other => other.Trigger.Overlaps(command.Trigger));
        if (position < 0)
        {
            return command;
        }

        var phrase = TriggerPhrase(command.Trigger, action);
        var other = commands[position];
        if (command.IsActive && !other.IsActive)
        {
            commands[position] = other with { Trigger = Trigger.None };
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, other.Name, $"'{other.Name}' and '{command.Name}' in '{groupName}' both use {phrase}; the inactive '{other.Name}' is imported without it."));
            return command;
        }

        _warnings.Add(new ImportWarning(ImportSeverity.Warning, command.Name, $"'{other.Name}' already uses {phrase} in '{groupName}'; '{command.Name}' is imported without it."));
        return command with { Trigger = Trigger.None };
    }

    /// <summary>"gesture 'Up'", "wheel down", "Shift + gesture 'Up'", "Shift + click": the trigger as SP.net users name it.</summary>
    private static string TriggerPhrase(Trigger trigger, JsonElement action)
    {
        var text = trigger.Describe(HostPlatform.Windows);
        return trigger is Trigger.GestureTrigger
            ? text.Replace(trigger.KindPhrase, $"gesture '{JsonRead.Text(action, StrokesPlusJson.Action.GestureName)}'", StringComparison.Ordinal)
            : text;
    }
}
