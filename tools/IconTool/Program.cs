using SkiaSharp;

// Regenerates every icon the app ships from one master image (design/app-icon/exports/app-icon.png, 1024 px).
// Outputs and their uses are listed in README.md. Run from the repo root:
//   dotnet run --project tools/IconTool [master.png] [app project folder]
var master = args.Length > 0 ? args[0] : Path.Combine("design", "app-icon", "exports", "app-icon.png");
var app = args.Length > 1 ? args[1] : Path.Combine("src", "Augram.App");

// A hand-drawn menu-bar master wins over the template derived from the app icon (same rule as Eyeris).
var macTrayMaster = Path.Combine(Path.GetDirectoryName(master) ?? ".", "app-icon-mac-tray.png");

using var source = Load(master);
using var traySource = File.Exists(macTrayMaster) ? Load(macTrayMaster) : null;

var packaging = Path.Combine(app, "Icons");
var assets = Path.Combine(app, "Assets");
Directory.CreateDirectory(packaging);
Directory.CreateDirectory(Path.Combine(assets, "Icons"));

Write(Path.Combine(packaging, "augram.ico"), IconContainers.Ico(IconSizes.Ico.Select(size => (size, IconImages.Render(source, size))).ToList()));
Write(Path.Combine(packaging, "augram.icns"), IconContainers.Icns(IconSizes.Icns.Select(entry => (entry.Type, IconImages.Render(source, entry.Size))).ToList()));
Write(Path.Combine(assets, "Icons", "augram-256.png"), IconImages.Render(source, 256));
Write(Path.Combine(assets, "tray-enabled.png"), IconImages.Render(source, IconSizes.Tray));
Write(Path.Combine(assets, "tray-disabled.png"), IconImages.Render(source, IconSizes.Tray, IconImages.DisabledOpacity, greyscale: true));
Write(Path.Combine(assets, "tray-enabled.ico"), IconContainers.Ico(IconSizes.TrayIco.Select(size => (size, IconImages.Render(source, size))).ToList()));
Write(Path.Combine(assets, "tray-disabled.ico"), IconContainers.Ico(IconSizes.TrayIco.Select(size => (size, IconImages.Render(source, size, IconImages.DisabledOpacity, greyscale: true))).ToList()));
Write(Path.Combine(assets, "tray-mac-enabled.png"), IconImages.Template(traySource ?? source, IconSizes.MacTray, 1f, traySource is not null));
Write(Path.Combine(assets, "tray-mac-disabled.png"), IconImages.Template(traySource ?? source, IconSizes.MacTray, IconImages.DisabledOpacity, traySource is not null));
// Options › General › Colour menu-bar icon (as in Eyeris): the app icon itself in the menu bar, greyed when disabled.
Write(Path.Combine(assets, "tray-mac-colour-enabled.png"), IconImages.Render(source, IconSizes.MacTray));
Write(Path.Combine(assets, "tray-mac-colour-disabled.png"), IconImages.Render(source, IconSizes.MacTray, IconImages.DisabledOpacity, greyscale: true));
Console.WriteLine($"from {master}{(traySource is null ? string.Empty : $" and {macTrayMaster}")}");

static SKBitmap Load(string path) => SKBitmap.Decode(path) ?? throw new InvalidOperationException($"Cannot read {path} as an image.");

static void Write(string path, byte[] bytes)
{
    File.WriteAllBytes(path, bytes);
    Console.WriteLine($"{path} ({bytes.Length} bytes)");
}
