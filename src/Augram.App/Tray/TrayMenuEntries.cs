using Augram.Platform.MacOS;
using Avalonia.Controls;

namespace Augram.App.Tray;

/// <summary>The tray's <see cref="NativeMenu"/> as the entries <see cref="MacStatusItem"/> shows (<see cref="MacTrayHost"/>); plain data, so it is tested on every platform.</summary>
internal static class TrayMenuEntries
{
    /// <summary>The menu as it is now: separators, and each item with its header, check mark, enabled state and click.</summary>
    public static IReadOnlyList<MacMenuEntry> Of(NativeMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        // A separator is a NativeMenuItem too (header "-"), so it is matched first.
        return menu.Items.Select(item => item is NativeMenuItem entry and not NativeMenuItemSeparator
            ? new MacMenuEntry(entry.Header ?? string.Empty, entry.IsChecked, entry.IsEnabled, () => Click(entry))
            : MacMenuEntry.Separator).ToList();
    }

    /// <summary>Raises the item's Click as Avalonia's own menu exporters do, so the handlers <see cref="AppTray"/> attached run.</summary>
    private static void Click(NativeMenuItem item) => ((INativeMenuItemExporterEventsImplBridge)item).RaiseClicked();
}
