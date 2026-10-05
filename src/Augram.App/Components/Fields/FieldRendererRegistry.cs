using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.Fields;

/// <summary>
/// Field kind → renderer. <see cref="Default"/> is filled by scanning this assembly for
/// <see cref="IFieldRenderer"/> implementations, so a new kind registers itself by existing.
/// <c>SectionForm</c> only ever calls <see cref="Build"/>; it never names a kind.
/// </summary>
public sealed class FieldRendererRegistry
{
    private readonly Dictionary<string, IFieldRenderer> _renderers = new(StringComparer.Ordinal);

    public FieldRendererRegistry(IEnumerable<IFieldRenderer> renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);
        foreach (var renderer in renderers)
        {
            if (!_renderers.TryAdd(renderer.Kind, renderer))
            {
                throw new InvalidOperationException($"Two renderers claim field kind '{renderer.Kind}'.");
            }
        }
    }

    public static FieldRendererRegistry Default { get; } = new(ScanAssembly());

    public IReadOnlyCollection<string> Kinds => _renderers.Keys;

    public bool Supports(Field field) => _renderers.ContainsKey(field.Kind);

    public Control Build(Field field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (!_renderers.TryGetValue(field.Kind, out var renderer))
        {
            throw new InvalidOperationException(
                $"No renderer for field kind '{field.Kind}'. Add one under Components/Fields/{field.Kind}/ implementing IFieldRenderer.");
        }

        return renderer.Build(field);
    }

    private static IEnumerable<IFieldRenderer> ScanAssembly() =>
        typeof(FieldRendererRegistry).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IFieldRenderer).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (IFieldRenderer)Activator.CreateInstance(type)!);
}
