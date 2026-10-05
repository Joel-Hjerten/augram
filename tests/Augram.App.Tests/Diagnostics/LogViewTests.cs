using Augram.App.Components.ItemList;
using Augram.App.Declarations;
using Augram.App.Screens;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Diagnostics;

public sealed class LogViewTests
{
    [AvaloniaFact]
    public void ShowsAnEventAfterItIsLogged()
    {
        var (list, sink, _, _) = Show();
        Assert.Empty(list.Rows);

        sink.Write(Event(EventLevel.Info, "hook", "Hook installed"));

        var row = Assert.Single(list.Rows);
        Assert.Equal("Hook installed", ((LogEvent)row).Message);
    }

    [AvaloniaFact]
    public void FiltersByMinimumLevelAndBySource()
    {
        var (list, sink, vm, _) = Show();
        sink.Write(Event(EventLevel.Debug, "hook", "trace-ish"));
        sink.Write(Event(EventLevel.Warning, "capture", "slow"));
        sink.Write(Event(EventLevel.Error, "hook", "lost"));

        vm.LevelFilter = "Warning";
        list.Refresh();
        Assert.Equal(2, list.Rows.Count);

        vm.SourceFilter = "hook";
        list.Refresh();
        Assert.Equal("lost", ((LogEvent)Assert.Single(list.Rows)).Message);
    }

    [AvaloniaFact]
    public void CopiesTheLastLinesInFileFormat()
    {
        var (_, sink, vm, clipboard) = Show();
        sink.Write(Event(EventLevel.Info, "app", "App started"));

        vm.CopyLastLinesAsync().GetAwaiter().GetResult();

        Assert.Contains("INFO  app       App started", clipboard.Text, StringComparison.Ordinal);
    }

    private static LogEvent Event(EventLevel level, string source, string message) =>
        new(DateTimeOffset.Now, level, source, message);

    private static (ItemList List, InMemorySink Sink, LogViewModel Vm, FakeClipboard Clipboard) Show()
    {
        var sink = new InMemorySink();
        var clipboard = new FakeClipboard();
        var vm = new LogViewModel(sink, Path.Combine(Path.GetTempPath(), "augram-app-tests", "logs"), clipboard);
        var screen = (ListScreen)LogScreen.Declare(vm);
        var list = new ItemList { Spec = screen.Spec };
        var window = new Window { Content = list };
        window.Show();
        return (list, sink, vm, clipboard);
    }
}
