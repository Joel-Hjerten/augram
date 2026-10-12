using Augram.App.Components.GestureGrid;
using Augram.App.Components.GesturePicker;
using Augram.App.Dialogs;
using Augram.App.Import;
using Augram.App.Training;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Core.Steps;
using Augram.Core.Training;
using Augram.Core.Transfer;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Transfer.TransferTestData;

namespace Augram.App.Tests.Dialogs;

/// <summary>
/// Every dialog on one base (Joel, 2026-10-12; plan 0007 S19): the windows S10 left off the shell are <see cref="DialogWindow"/>s
/// with their content in the <see cref="DialogShell"/> and its margin, and Escape closes each once, so its own Cancel never fires
/// as well. The import review has the shell's margin too.
/// </summary>
public sealed class DialogBaseTests
{
    [AvaloniaFact]
    public void TheMovedDialogsAreOnTheShellWithItsMargin_AndEscapeCancelsOnce()
    {
        var picker = new GesturePicker { Tiles = [.. StarterGestures.All().Select(GestureTileItem.From)] };
        var picked = new List<GesturePickerResult>();
        picker.Closed += (_, result) => picked.Add(result);
        EscapeClosesOnTheShell(new GesturePickerWindow(picker));
        Assert.Empty(picked);

        var session = new TrainingSession(new GestureLibrary(), () => RecognitionOptions.Default);
        var outcomes = new List<TrainingOutcome>();
        session.Ended += (_, outcome) => outcomes.Add(outcome);
        session.Begin(TrainingRequest.NewGesture);
        EscapeClosesOnTheShell(new TrainingWindow(new TrainingViewModel(session)));
        Assert.Equal([TrainingOutcome.Cancelled], outcomes);

        var import = new ImportViewModel(new GestureLibrary(), new MappingStore(), NullEventLog.Instance);
        var finished = 0;
        import.Finished += (_, _) => finished++;
        EscapeClosesOnTheShell(new ImportDialog(import));
        Assert.Equal(0, finished);

        var mine = Configuration();
        using var config = Session(mine);
        var review = new AugramImportWindow(new AugramImportViewModel(config, TransferSerializer.Read(TransferSerializer.Write(Exporter.Export(ExportScope.GesturesOnly, mine)), StepRegistry.BuiltIn), "gestures.augram.json", new ListEventLog()));
        review.Show();
        Assert.Equal(ShellMargin, review.View.Margin);
        review.Close();
    }

    private static Thickness ShellMargin
        => Application.Current!.TryGetResource("Pad.L", null, out var margin) ? (Thickness)margin! : throw new InvalidOperationException("No Pad.L token.");

    private static void EscapeClosesOnTheShell(DialogWindow window)
    {
        window.Show();
        var shell = window.GetVisualDescendants().OfType<DialogShell>().Single();
        Assert.Equal(ShellMargin, shell.Margin);
        var escape = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };

        window.RaiseEvent(escape);

        Assert.True(escape.Handled);
        Assert.False(window.IsVisible);
    }
}
