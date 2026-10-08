using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Imported;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.TypeText;
using Augram.Core.Steps.WindowOp;

namespace Augram.Core.Steps;

/// <summary>
/// The step types Augram knows, keyed by <see cref="IStepType.Key"/>. <see cref="BuiltIn"/> is the
/// static registration list ADR-0002 §4 allows: adding a type is one folder plus one line here, and
/// that line is the only place outside the folder that names the type. Config reading, the step
/// picker and the executor all go through the registry.
/// </summary>
public sealed class StepRegistry
{
    private readonly Dictionary<string, IStepType> _byKey;

    public StepRegistry(IEnumerable<IStepType> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        All = types.ToList();
        _byKey = new Dictionary<string, IStepType>(StringComparer.Ordinal);
        foreach (var type in All)
        {
            if (!_byKey.TryAdd(type.Key, type))
            {
                throw new ArgumentException($"Two step types share the key '{type.Key}'.", nameof(types));
            }
        }
    }

    /// <summary>Every shipped type, in picker order within each category. Each type's folder adds itself here.</summary>
    public static StepRegistry BuiltIn { get; } = new(
    [
        WindowOpStepType.Instance,
        MediaKeyStepType.Instance,
        HotkeyStepType.Instance,
        TypeTextStepType.Instance,
        DelayStepType.Instance,
        ImportedStepType.Instance,
    ]);

    public IReadOnlyList<IStepType> All { get; }

    public IStepType? Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _byKey.GetValueOrDefault(key);
    }

    public IStepType Require(string key)
        => Find(key) ?? throw new StepFormatException($"Unknown step type '{key}'.");
}
