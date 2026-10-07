using Augram.Core.Steps;
using Avalonia.Controls;

namespace Augram.App.Components.Steps;

/// <summary>
/// Step type key → form. <see cref="Default"/> is filled by scanning this assembly for
/// <see cref="IStepForm"/> implementations, like the field renderers, so adding a step type's form is
/// adding a folder. The step list only ever calls <see cref="Build"/>; it never names a type.
/// </summary>
public sealed class StepFormRegistry
{
    private readonly Dictionary<string, IStepForm> _forms = new(StringComparer.Ordinal);

    public StepFormRegistry(IEnumerable<IStepForm> forms)
    {
        ArgumentNullException.ThrowIfNull(forms);
        foreach (var form in forms)
        {
            if (!_forms.TryAdd(form.TypeKey, form))
            {
                throw new InvalidOperationException($"Two forms claim step type '{form.TypeKey}'.");
            }
        }
    }

    public static StepFormRegistry Default { get; } = new(ScanAssembly());

    public IReadOnlyCollection<string> TypeKeys => _forms.Keys;

    public bool Supports(IStepType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _forms.ContainsKey(type.Key);
    }

    public Control Build(IStep step, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(changed);
        if (!_forms.TryGetValue(step.Type.Key, out var form))
        {
            throw new InvalidOperationException(
                $"No form for step type '{step.Type.Key}'. Add one under Components/Steps/<Name>/ implementing IStepForm.");
        }

        return form.Build(step, changed);
    }

    private static IEnumerable<IStepForm> ScanAssembly() =>
        typeof(StepFormRegistry).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IStepForm).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (IStepForm)Activator.CreateInstance(type)!);
}
