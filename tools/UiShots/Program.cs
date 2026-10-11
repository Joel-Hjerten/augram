using Augram.App;
using Augram.App.Components.Shell;
using Augram.App.Navigation;
using Augram.App.Themes;
using Augram.App.ViewModels;
using Augram.App.Views;
using Augram.Core.Config;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;

// Renders Augram's main window headless to PNG files: Skia drawing, no desktop window, no hook, no overlay, no engine.
// Usage and options: README.md next to this file.
var options = Options.Parse(args);
Directory.CreateDirectory(options.Out);
var work = Path.Combine(Path.GetTempPath(), "augram-shots", Guid.NewGuid().ToString("N")[..8]);
var config = Path.Combine(work, "config");
Directory.CreateDirectory(config);
if (options.ConfigFrom is { } from)
{
    // A copy, so nothing is ever written to the real config folder.
    foreach (var file in Directory.EnumerateFiles(from, "*.json"))
    {
        File.Copy(file, Path.Combine(config, Path.GetFileName(file)));
    }
}

var services = CompositionRoot.Build(logsFolder: Path.Combine(work, "logs"), configFolder: config);
AppBuilder.Configure(() => new App(services))
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

// What App.StartDesktop would link up (it does not run without a desktop lifetime).
GlyphColourLink.Follow(services.GetRequiredService<SettingsStore>(), Application.Current!.Resources);

var window = services.GetRequiredService<MainWindow>();
window.Width = options.Width;
window.Height = options.Height;
window.Show();
var shell = window.GetVisualDescendants().OfType<Shell>().Single();
var keys = options.Keys.Count > 0 ? options.Keys : LeafKeys(services.GetRequiredService<MainWindowViewModel>().Registry, options.Gallery);

foreach (var (name, variant) in options.Themes)
{
    Application.Current.RequestedThemeVariant = variant;
    // Solid until the window backdrop is wired; a real window shows the system's glass under the tint instead.
    window.Background = window.TryFindResource("Layer.Window", variant, out var tint) && tint is Color colour ? new SolidColorBrush(colour) : null;
    foreach (var key in keys)
    {
        shell.SelectedKey = key;
        Settle();
        var frame = window.CaptureRenderedFrame();
        var path = Path.Combine(options.Out, $"{name}-{key}.png");
        frame?.Save(path);
        Console.WriteLine(frame is null ? $"(no frame) {path}" : path);
    }
}

return 0;

static void Settle()
{
    for (var i = 0; i < 3; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}

static List<string> LeafKeys(NavigationRegistry registry, bool gallery)
{
    var keys = new List<string>();
    foreach (var entry in registry.Entries)
    {
        var isGallery = entry.Title == "Gallery";
        if (isGallery && !gallery)
        {
            continue;
        }

        if (entry.HasSubEntries)
        {
            keys.AddRange(entry.SubEntries!.Select(sub => sub.Key));
        }
        else
        {
            keys.Add(entry.Key);
        }
    }

    return keys;
}

internal sealed record Options(string Out, string? ConfigFrom, IReadOnlyList<(string Name, ThemeVariant Variant)> Themes, double Width, double Height, bool Gallery, IReadOnlyList<string> Keys)
{
    public static Options Parse(string[] args)
    {
        var output = Path.Combine(Path.GetTempPath(), "augram-shots", "out");
        string? configFrom = null;
        var themes = new List<(string, ThemeVariant)> { ("dark", ThemeVariant.Dark), ("light", ThemeVariant.Light) };
        double width = 1000, height = 680;
        var gallery = false;
        var keys = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out":
                    output = args[++i];
                    break;
                case "--config":
                    configFrom = args[++i];
                    break;
                case "--theme":
                    themes = args[++i] switch
                    {
                        "dark" => [("dark", ThemeVariant.Dark)],
                        "light" => [("light", ThemeVariant.Light)],
                        _ => themes,
                    };
                    break;
                case "--size":
                    var parts = args[++i].Split('x');
                    (width, height) = (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "--gallery":
                    gallery = true;
                    break;
                default:
                    keys.Add(args[i]);
                    break;
            }
        }

        return new Options(output, configFrom, themes, width, height, gallery, keys);
    }
}
