using Augram.App.Declarations;
using Avalonia.Controls;

namespace Augram.App.Components.ScreenHost;

/// <summary>Screen declaration type → renderer. Filled by scanning this assembly for <see cref="IScreenRenderer"/>s.</summary>
public sealed class ScreenRendererRegistry
{
    private readonly Dictionary<Type, IScreenRenderer> _renderers = [];

    public ScreenRendererRegistry(IEnumerable<IScreenRenderer> renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);
        foreach (var renderer in renderers)
        {
            if (!_renderers.TryAdd(renderer.ScreenType, renderer))
            {
                throw new InvalidOperationException($"Two renderers claim screen type '{renderer.ScreenType.Name}'.");
            }
        }
    }

    public static ScreenRendererRegistry Default { get; } = new(ScanAssembly());

    public Control Build(ScreenDeclaration screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        if (!_renderers.TryGetValue(screen.GetType(), out var renderer))
        {
            throw new InvalidOperationException($"No renderer for screen type '{screen.GetType().Name}'. Add one under Components/ScreenHost/.");
        }

        return renderer.Build(screen);
    }

    private static IEnumerable<IScreenRenderer> ScanAssembly() =>
        typeof(ScreenRendererRegistry).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && typeof(IScreenRenderer).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (IScreenRenderer)Activator.CreateInstance(type)!);
}
