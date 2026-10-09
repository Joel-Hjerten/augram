using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using Augram.Platform.MacOS;
using Avalonia.Controls;

namespace Augram.App.Tray;

/// <summary>
/// The macOS menu-bar item (Joel, 2026-10-09): Augram's own <see cref="MacStatusItem"/>, so a left click reaches the tray
/// (toggle, and open on a double click, as on Windows) and the menu opens on a right click or a Control-click. The menu is
/// the same <see cref="NativeMenu"/> the other platforms show, read afresh each time it opens.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacTrayHost : ITrayHost
{
    private readonly NativeMenu _menu;
    private readonly MacStatusItem _item = new();
    private readonly ConditionalWeakTable<WindowIcon, byte[]> _pngs = new();

    public MacTrayHost(NativeMenu menu)
    {
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _item.Clicked += OnClicked;
        _item.MenuEntries = Entries;
    }

    public event EventHandler? Clicked;

    public void Show(WindowIcon icon, bool isTemplate, string toolTip)
    {
        ArgumentNullException.ThrowIfNull(icon);
        _item.SetImage(_pngs.GetValue(icon, Png), isTemplate);
        _item.SetToolTip(toolTip);
    }

    public void Dispose()
    {
        _item.Clicked -= OnClicked;
        _item.Dispose();
    }

    private IReadOnlyList<MacMenuEntry> Entries() => TrayMenuEntries.Of(_menu);

    private static byte[] Png(WindowIcon icon)
    {
        using var stream = new MemoryStream();
        icon.Save(stream);
        return stream.ToArray();
    }

    private void OnClicked(object? sender, EventArgs e) => Clicked?.Invoke(this, e);
}
