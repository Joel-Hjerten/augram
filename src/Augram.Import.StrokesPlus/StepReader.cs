using System.Text.Json;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// Reads one SP.net <c>Steps[]</c> entry into a <see cref="CommandStep"/> authored on Windows (plan
/// 0001 §C1): the window methods to <see cref="WindowOpStep"/>, <c>Delay</c> to <see cref="DelayStep"/>,
/// <c>SendVKey</c> with a media key to <see cref="MediaKeyStep"/>, <c>SendHotKey</c> and any other
/// <c>SendVKey</c> to a <c>HotkeyStep</c> through <see cref="HotkeyMapping"/>, and everything else to an
/// <see cref="ImportedStep"/> placeholder that keeps the method and its parameters until the real type
/// lands. Placeholders are counted per method and reported once per file by <see cref="ReportPlaceholders"/>.
/// </summary>
internal sealed class StepReader
{
    private const string NoMethod = "(no method)";

    private readonly List<ImportWarning> _warnings;
    private readonly Dictionary<string, int> _placeholders = new(StringComparer.Ordinal);

    public StepReader(List<ImportWarning> warnings)
    {
        _warnings = warnings;
    }

    public List<CommandStep> ReadAll(JsonElement action, string commandName)
    {
        var steps = new List<CommandStep>();
        if (!JsonRead.TryArray(action, StrokesPlusJson.Action.Steps, out var array))
        {
            return steps;
        }

        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            index++;
            if (element.ValueKind != JsonValueKind.Object)
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, $"Step {index} is not an object; skipped."));
                continue;
            }

            if (SendKeys(element, commandName) is { } typed)
            {
                steps.AddRange(typed);
                continue;
            }

            steps.Add(Read(element, commandName));
        }

        return steps;
    }

    /// <summary>
    /// A <c>SendKeys</c> step as the several steps its key string makes (text runs, hotkeys, delays); null for any other
    /// method, and for a string with a part that does not map, which then imports as a placeholder with a warning per part.
    /// </summary>
    private List<CommandStep>? SendKeys(JsonElement step, string commandName)
    {
        if (JsonRead.Text(step, StrokesPlusJson.Step.Method) != StrokesPlusJson.Method.SendKeys
            || TextMapping.FromSendKeys(MethodParameterReader.Read(step)) is not { } result)
        {
            return null;
        }

        if (!result.IsClean || result.Steps.Count == 0)
        {
            foreach (var warning in result.Warnings)
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, $"SendKeys: {warning}"));
            }

            return null;
        }

        var isActive = JsonRead.Flag(step, StrokesPlusJson.Step.Active, whenAbsent: true);
        return [.. result.Steps.Select(typed => new CommandStep(typed, HostPlatform.Windows, IsActive: isActive))];
    }

    public CommandStep Read(JsonElement step, string commandName)
    {
        var method = JsonRead.Text(step, StrokesPlusJson.Step.Method);
        var description = JsonRead.Text(step, StrokesPlusJson.Step.Description);
        var parameters = MethodParameterReader.Read(step);
        var isActive = JsonRead.Flag(step, StrokesPlusJson.Step.Active, whenAbsent: true);
        return new CommandStep(Map(method, description, parameters, commandName), HostPlatform.Windows, IsActive: isActive);
    }

    /// <summary>The script of a script-only action as a placeholder step, so the script is visible on the step list.</summary>
    public CommandStep Script(string script, string description)
    {
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal) { [StrokesPlusJson.Method.ScriptParameter] = script };
        return new CommandStep(Placeholder(StrokesPlusJson.Method.Script, description, parameters), HostPlatform.Windows);
    }

    /// <summary>One Info line per method imported as placeholders, with the count and when the real type arrives.</summary>
    public void ReportPlaceholders()
    {
        foreach (var (method, count) in _placeholders)
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Info, method, $"{count} {method} step(s) imported as placeholders; {PlaceholderReason(method)}"));
        }

        _placeholders.Clear();
    }

    private IStep Map(string method, string description, IReadOnlyDictionary<string, string> parameters, string commandName) => method switch
    {
        StrokesPlusJson.Method.CloseWindow => new WindowOpStep(WindowOperation.Close),
        StrokesPlusJson.Method.MinimizeWindow => new WindowOpStep(WindowOperation.Minimize),
        StrokesPlusJson.Method.MaximizeOrRestoreWindow => new WindowOpStep(WindowOperation.MaximizeOrRestore),
        StrokesPlusJson.Method.ToggleWindowAlwaysOnTop => new WindowOpStep(WindowOperation.ToggleAlwaysOnTop),
        StrokesPlusJson.Method.SetWindowSize => SetSize(description, parameters, commandName),
        StrokesPlusJson.Method.InvokeObjectMethodByName => InvokedMethod(description, parameters),
        StrokesPlusJson.Method.Delay => Delay(description, parameters, commandName),
        StrokesPlusJson.Method.SendVKey => VirtualKey(description, parameters),
        StrokesPlusJson.Method.SendHotKey => (IStep?)HotkeyMapping.FromSendHotKey(parameters) ?? Placeholder(StrokesPlusJson.Method.SendHotKey, description, parameters),
        StrokesPlusJson.Method.SendString => (IStep?)TextMapping.FromSendString(parameters) ?? Placeholder(StrokesPlusJson.Method.SendString, description, parameters),
        StrokesPlusJson.Method.Run => (IStep?)RunMapping.FromRun(parameters) ?? Placeholder(StrokesPlusJson.Method.Run, description, parameters),
        _ => Placeholder(method, description, parameters),
    };

    private IStep SetSize(string description, IReadOnlyDictionary<string, string> parameters, string commandName)
    {
        if (MethodParameterReader.TryInt32(parameters, StrokesPlusJson.Method.WidthParameter, out var width)
            && MethodParameterReader.TryInt32(parameters, StrokesPlusJson.Method.HeightParameter, out var height)
            && width > 0
            && height > 0)
        {
            return new WindowOpStep(WindowOperation.SetSize, new WindowSize(width, height));
        }

        _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, "SetWindowSize has no usable width and height; imported as a placeholder."));
        return Placeholder(StrokesPlusJson.Method.SetWindowSize, description, parameters);
    }

    private IStep InvokedMethod(string description, IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters.TryGetValue(StrokesPlusJson.Method.MethodNameParameter, out var name)
            && string.Equals(name.Trim(), StrokesPlusJson.Method.CenterMethodName, StringComparison.OrdinalIgnoreCase))
        {
            return new WindowOpStep(WindowOperation.Center);
        }

        return Placeholder(StrokesPlusJson.Method.InvokeObjectMethodByName, description, parameters);
    }

    private IStep Delay(string description, IReadOnlyDictionary<string, string> parameters, string commandName)
    {
        if (!MethodParameterReader.TryInt32(parameters, StrokesPlusJson.Method.MillisecondsParameter, out var milliseconds))
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, "Delay has no usable milliseconds value; imported as a placeholder."));
            return Placeholder(StrokesPlusJson.Method.Delay, description, parameters);
        }

        var clamped = Math.Clamp(milliseconds, DelayStepType.MinMilliseconds, DelayStepType.MaxMilliseconds);
        if (clamped != milliseconds)
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, $"Delay of {milliseconds} ms clamped to {clamped} ms."));
        }

        return new DelayStep(clamped);
    }

    private IStep VirtualKey(string description, IReadOnlyDictionary<string, string> parameters)
    {
        if (MethodParameterReader.TryInt32(parameters, StrokesPlusJson.Method.VirtualKeyParameter, out var key) && HotkeyMapping.MediaKeyOf(key) is { } kind)
        {
            return new MediaKeyStep(kind);
        }

        return (IStep?)HotkeyMapping.FromSendVKey(parameters) ?? Placeholder(StrokesPlusJson.Method.SendVKey, description, parameters);
    }

    private ImportedStep Placeholder(string method, string description, IReadOnlyDictionary<string, string> parameters)
    {
        var key = method.Length == 0 ? NoMethod : method;
        _placeholders[key] = _placeholders.GetValueOrDefault(key) + 1;
        return new ImportedStep(method, description.Length == 0 ? key : description, parameters);
    }

    private static string PlaceholderReason(string method) => method switch
    {
        StrokesPlusJson.Method.SendHotKey => "their key could not be mapped to an Augram key.",
        StrokesPlusJson.Method.SendKeys => "part of their key string has no Augram equivalent (see the warnings).",
        StrokesPlusJson.Method.SendString => "they have no text.",
        StrokesPlusJson.Method.Run => "they have no command line.",
        StrokesPlusJson.Method.MouseClick => "mouse clicks arrive in a later version.",
        StrokesPlusJson.Method.SendVKey => "their virtual key has no Augram key.",
        StrokesPlusJson.Method.SendAltDown or StrokesPlusJson.Method.SendAltUp
            or StrokesPlusJson.Method.SendWinDown or StrokesPlusJson.Method.SendWinUp
            or StrokesPlusJson.Method.ConsumePhysicalInput => "candidates for a hold-modifier step later.",
        StrokesPlusJson.Method.Script => "Augram has no scripting; the script is kept for reference.",
        NoMethod => "the step names no method.",
        _ => "no Augram equivalent yet.",
    };
}
