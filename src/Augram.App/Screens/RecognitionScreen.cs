using Augram.App.Declarations;
using Augram.App.ViewModels;
using Augram.Core.Diagnostics;

namespace Augram.App.Screens;

/// <summary>Diagnostics › Recognition (A15): one row per stroke with its top matches.</summary>
public static class RecognitionScreen
{
    public static ScreenDeclaration Declare(RecognitionViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new ListScreen("Recognition", new ListSpec(
            "Recognition",
            Columns:
            [
                new ListColumn("Time", row => RecognitionViewModel.Time((RecognitionLogEntry)row), 90),
                new ListColumn("Points", row => RecognitionViewModel.Points((RecognitionLogEntry)row), 60),
                new ListColumn("Duration", row => RecognitionViewModel.Duration((RecognitionLogEntry)row), 80),
                new ListColumn("Top matches", row => RecognitionViewModel.TopMatches((RecognitionLogEntry)row)),
                new ListColumn("Result", row => RecognitionViewModel.Result((RecognitionLogEntry)row), 240),
            ],
            Source: vm.Source,
            AutoScroll: true));
    }
}
