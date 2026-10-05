using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Core.Diagnostics;
using Augram.Engine.Diagnostics;

namespace Augram.App.Screens;

/// <summary>Diagnostics › Log (N4): the live tail with level/source filters and the two paste-at-an-agent actions.</summary>
public static class LogScreen
{
    public static ScreenDeclaration Declare(LogViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new ListScreen("Log", new ListSpec(
            "Log",
            Columns:
            [
                new ListColumn("Time", row => LogViewModel.Time((LogEvent)row), 90),
                new ListColumn("Level", row => LogLineFormatter.Label(((LogEvent)row).Level), 60),
                new ListColumn("Source", row => ((LogEvent)row).Source, 90),
                new ListColumn("Message", row => LogViewModel.Detail((LogEvent)row)),
            ],
            Source: vm.Source,
            Toolbar:
            [
                new ListAction("Open log folder", vm.OpenLogFolder),
                new ListAction("Copy last 200 lines", () => _ = vm.CopyLastLinesAsync()),
            ],
            Filters:
            [
                new ListFilter("Level", LogViewModel.Levels,
                    new DelegateBinding<string>(() => vm.LevelFilter, v => vm.LevelFilter = v, vm), LogViewModel.MatchesLevel),
                new ListFilter("Source", LogViewModel.Sources,
                    new DelegateBinding<string>(() => vm.SourceFilter, v => vm.SourceFilter = v, vm), LogViewModel.MatchesSource),
            ],
            AutoScroll: true));
    }
}
