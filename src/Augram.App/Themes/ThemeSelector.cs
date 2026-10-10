using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Augram.App.Themes;

/// <summary>
/// Picks the theme by name (ADR-0002 §5b: switching is one line). <see cref="Active"/> is the one
/// line; <c>App</c> loads it at start and the inspector reports it.
/// </summary>
public static class ThemeSelector
{
    public const string Wireframe = "Wireframe";
    public const string Default = "Default";

    /// <summary>The theme the app runs on. Wireframe until D2 is decided.</summary>
    public static string Active { get; } = Wireframe;

    public static IReadOnlyList<string> Names { get; } = [Wireframe, Default];

    public static Uri UriFor(string name)
    {
        if (!Names.Contains(name, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unknown theme '{name}'. Known: {string.Join(", ", Names)}.", nameof(name));
        }

        return new Uri($"avares://Augram/Themes/{name}/{name}.axaml");
    }

    public static IStyle Load(string name) => (IStyle)AvaloniaXamlLoader.Load(UriFor(name));
}
