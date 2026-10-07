using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Imported;

/// <summary>
/// The "Imported (not supported yet)" placeholder type: key <c>imported</c>, category
/// <see cref="StepCategory.Other"/> so the picker never offers it (it only ever enters a command
/// through the importer or the config file). Parameters:
/// <c>{ "method": "SendAltDown", "description": "…", "parameters": { "name": "value", … } }</c>,
/// every parameter value a string. Platform-neutral in the sense that there is nothing to convert.
/// </summary>
public sealed class ImportedStepType : IStepType
{
    private ImportedStepType()
    {
    }

    public static ImportedStepType Instance { get; } = new();

    public string Key => "imported";

    public string DisplayName => "Imported (not supported yet)";

    public StepCategory Category => StepCategory.Other;

    public bool IsPlatformNeutral => true;

    /// <summary>The same on every platform: nothing to convert.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => new ImportedStep(string.Empty, string.Empty, ImportedStep.NoParameters);

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var method = StepParameters.ReadString(parameters, "method") ?? string.Empty;
        var description = StepParameters.ReadString(parameters, "description") ?? string.Empty;
        return new ImportedStep(method, description, ReadSourceParameters(parameters));
    }

    public JsonObject Write(IStep step)
    {
        var imported = StepParameters.Expect<ImportedStep>(step, this);
        var sourceParameters = new JsonObject();
        foreach (var (name, value) in imported.Parameters)
        {
            sourceParameters[name] = value;
        }

        return new JsonObject
        {
            ["method"] = imported.SourceMethod,
            ["description"] = imported.Description,
            ["parameters"] = sourceParameters,
        };
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ImportedExecutor.Execute(StepParameters.Expect<ImportedStep>(step, this), context);
    }

    private static IReadOnlyDictionary<string, string> ReadSourceParameters(JsonObject parameters)
    {
        var node = parameters["parameters"];
        if (node is null)
        {
            return ImportedStep.NoParameters;
        }

        if (node is not JsonObject source)
        {
            throw new StepFormatException("'parameters' must be an object.");
        }

        if (source.Count == 0)
        {
            return ImportedStep.NoParameters;
        }

        var read = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in source)
        {
            if (value is not JsonValue jsonValue || !jsonValue.TryGetValue(out string? text))
            {
                throw new StepFormatException($"'parameters.{name}' must be a string.");
            }

            read[name] = text;
        }

        return read;
    }
}
