using Augram.App.Tray;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Tray;

/// <summary>The macOS menu-bar item shows the tray's own menu (Joel, 2026-10-09): same items, same state, and a pick runs the item's Click.</summary>
public sealed class TrayMenuEntriesTests
{
    [AvaloniaFact]
    public void EveryItemKeepsItsTitleCheckMarkAndEnabledState_AndSeparatorsStay()
    {
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("Open"));
        menu.Items.Add(new NativeMenuItem("Enabled") { ToggleType = NativeMenuItemToggleType.CheckBox, IsChecked = true });
        menu.Items.Add(new NativeMenuItem("Start at login") { ToggleType = NativeMenuItemToggleType.CheckBox, IsEnabled = false });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem("Quit"));

        var entries = TrayMenuEntries.Of(menu);

        Assert.Equal(
            [("Open", false, true, false), ("Enabled", true, true, false), ("Start at login", false, false, false), ("", false, false, true), ("Quit", false, true, false)],
            entries.Select(entry => (entry.Title, entry.IsChecked, entry.IsEnabled, entry.IsSeparator)));
    }

    [AvaloniaFact]
    public void PickingAnEntryRunsTheItemsClick()
    {
        var clicks = 0;
        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => clicks++;
        var menu = new NativeMenu();
        menu.Items.Add(quit);

        TrayMenuEntries.Of(menu)[0].Invoke!();

        Assert.Equal(1, clicks);
    }

    [AvaloniaFact]
    public void TheMenuIsReadAtTheMomentItOpens()
    {
        var enabled = new NativeMenuItem("Enabled") { ToggleType = NativeMenuItemToggleType.CheckBox };
        var menu = new NativeMenu();
        menu.Items.Add(enabled);
        Assert.False(TrayMenuEntries.Of(menu)[0].IsChecked);

        enabled.IsChecked = true;
        menu.Items.Add(new NativeMenuItem("Sync now"));

        var entries = TrayMenuEntries.Of(menu);
        Assert.True(entries[0].IsChecked);
        Assert.Equal("Sync now", entries[1].Title);
    }
}
