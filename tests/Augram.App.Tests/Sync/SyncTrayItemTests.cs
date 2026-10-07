using System.Collections.Concurrent;
using System.Globalization;
using Augram.App.Tests.Sync.Support;
using Augram.App.Tray;
using Augram.Core.Sync;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Sync;

/// <summary>The tray's Sync now: in the menu only while a repository is set; the tooltip's short status.</summary>
public sealed class SyncTrayItemTests : SyncTestBase
{
    [AvaloniaFact]
    public void SyncNowIsInTheMenuOnlyWhileARepositoryIsSet()
    {
        var home = Machine("PC-HOME", url: null);
        var posted = new ConcurrentQueue<Action>();
        var quit = new NativeMenuItem("Quit");
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("Open"));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quit);
        using var item = new SyncTrayItem(home.Service, menu, posted.Enqueue);
        Assert.Equal("Sync now", item.Item.Header);
        Assert.False(item.IsShown);
        Assert.Equal(3, menu.Items.Count);
        Assert.Null(item.ShortStatus);

        home.Service.Start();
        home.Settings.SetSync(home.Settings.Current.Sync with { RepositoryUrl = SyncMachine.Url });
        home.WaitForRuns(1);
        Drain(posted);
        Assert.True(item.IsShown);
        Assert.Same(item.Item, menu.Items[1]);
        Assert.Same(quit, menu.Items[^1]);
        Assert.True(item.Item.IsEnabled);
        Assert.StartsWith("synced ", item.ShortStatus, StringComparison.Ordinal);

        home.Settings.SetSync(home.Settings.Current.Sync with { RepositoryUrl = null });
        home.WaitForRuns(2);
        Drain(posted);
        Assert.False(item.IsShown);
        Assert.Equal(3, menu.Items.Count);
        Assert.Null(item.ShortStatus);
    }

    [Fact]
    public void TheShortStatusFitsATooltip()
    {
        var at = new DateTimeOffset(2026, 10, 7, 14, 32, 0, TimeSpan.Zero);
        var time = at.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

        Assert.Null(SyncTrayItem.Short(false, false, false, null));
        Assert.Null(SyncTrayItem.Short(true, false, false, null));
        Assert.Equal("syncing…", SyncTrayItem.Short(true, true, false, null));
        Assert.Equal("sync paused", SyncTrayItem.Short(true, false, true, null));
        Assert.Equal($"synced {time}", SyncTrayItem.Short(true, false, false, new SyncReport(SyncStatus.Applied, at)));
        Assert.Equal($"sync failed {time}", SyncTrayItem.Short(true, false, false, new SyncReport(SyncStatus.Failed, at) { Error = "offline" }));
    }

    private static void Drain(ConcurrentQueue<Action> posted)
    {
        while (posted.TryDequeue(out var action))
        {
            action();
        }
    }
}
