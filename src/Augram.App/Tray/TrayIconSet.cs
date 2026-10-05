using Avalonia.Controls;
using Avalonia.Platform;

namespace Augram.App.Tray;

/// <summary>The two placeholder tray icons (F7: the state must be unmistakable): a filled square when enabled, an outline when disabled.</summary>
public sealed class TrayIconSet
{
    private TrayIconSet(WindowIcon enabled, WindowIcon disabled)
    {
        Enabled = enabled;
        Disabled = disabled;
    }

    public WindowIcon Enabled { get; }

    public WindowIcon Disabled { get; }

    public static TrayIconSet Load() => new(LoadIcon("tray-enabled.png"), LoadIcon("tray-disabled.png"));

    private static WindowIcon LoadIcon(string file)
    {
        using var stream = AssetLoader.Open(new Uri("avares://Augram.App/Assets/" + file));
        return new WindowIcon(stream);
    }
}
