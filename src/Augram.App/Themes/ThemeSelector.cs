using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Augram.App.Themes;

/// <summary>
/// Picks the theme by name (ADR-0002 §5b: switching is one line). <see cref="Active"/> is the one
/// line; <c>App</c> loads it at start and the inspector reports it. Default, the glass look, is the only theme since
/// the Wireframe theme was retired (ADR-0002 §5b, amended 2026-10-11); a future skin starts as a copy of it.
/// </summary>
public static class ThemeSelector
{
    public const string Default = "Default";

    /// <summary>The theme the app runs on: Default, the glass look (plan 0006).</summary>
    public static string Active { get; } = Default;

    /// <summary>Whether the active theme draws the window's title bar itself, with the top-level tabs in it (plan 0006 decision 5).</summary>
    public static bool DrawsTitleBar => Active == Default;

    public static IReadOnlyList<string> Names { get; } = [Default];

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
