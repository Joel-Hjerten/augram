using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Unknown;

namespace Augram.Core.Config;

/// <summary>
/// Reads the step envelopes of one command as <see cref="MappingJsonWriter"/> writes them, resolving
/// each <c>type</c> through the <see cref="StepRegistry"/> and the parameters through
/// <see cref="IStepType.Read"/>. A step whose type the registry lacks, or whose parameters its type
/// refuses, is kept as is (<see cref="UnknownStep"/>, Joel 2026-10-09) with one notice, and written back
/// unchanged when this build saves: a newer or hand-edited file never loses the whole configuration over
/// one step (F8), and an older build no longer deletes a newer one's steps. An override that fails the same way
/// is dropped on its own; the authored step stays. A malformed envelope (no <c>type</c>, <c>params</c>
/// not an object) is a format error like any other.
/// </summary>
internal sealed class CommandStepJsonReader
{
    private readonly StepRegistry _registry;
    private readonly Action<string>? _notice;

    public CommandStepJsonReader(StepRegistry registry, Action<string>? notice)
    {
        _registry = registry;
        _notice = notice;
    }

    /// <param name="steps">The command's <c>steps</c> array.</param>
    /// <param name="where">Names the command for notices: "command 'Close tab' in 'Chrome'".</param>
    public IReadOnlyList<CommandStep> ReadAll(JsonArray steps, string where)
    {
        var result = new List<CommandStep>(steps.Count);
        for (int i = 0; i < steps.Count; i++)
        {
            result.Add(Read(steps[i], $"step {i + 1} of {where}"));
        }

        return result;
    }

    private CommandStep Read(JsonNode? node, string where)
    {
        var envelope = JsonMembers.RequireObject(node, Capitalised(where));
        var key = JsonMembers.RequireString(envelope, "type", where);
        var parameters = Parameters(envelope["params"], "params", where);
        var type = _registry.Find(key);
        IStep step;
        if (type is null)
        {
            step = Keep(where, key, parameters, $"unknown step type '{key}'");
        }
        else
        {
            try
            {
                step = type.Read(parameters);
            }
            catch (StepFormatException ex)
            {
                step = Keep(where, key, parameters, ex.Message.TrimEnd('.'));
            }
        }

        // A step-level "overrides" member (the 2026-10-05 design, never written by a shipped version) is passed over: a
        // platform that needs other steps has a command-level own version instead (F8, 2026-10-07).
        return new CommandStep(
            step,
            JsonMembers.OptionalEnum(envelope, "authoredOn", HostPlatform.Windows, where),
            IsActive: JsonMembers.OptionalBool(envelope, "isActive", fallback: true, where));
    }

    private static JsonObject Parameters(JsonNode? node, string name, string where)
        => node is null ? [] : JsonMembers.RequireObject(node, $"'{name}' of {where}");

    /// <summary>The step as the file had it, with one notice; the sync counts these notices and skips such a file.</summary>
    private UnknownStep Keep(string where, string key, JsonObject parameters, string why)
    {
        _notice?.Invoke($"{Capitalised(where)} kept as is: {why}.");
        return UnknownStepType.Keep(key, parameters, why);
    }

    private static string Capitalised(string text)
        => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
