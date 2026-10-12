using System.Text.RegularExpressions;
using Augram.App.Components.FormDialog;
using Augram.App.Components.GestureGrid;
using Augram.App.Components.GesturePicker;
using Augram.App.Dialogs;
using Augram.App.Import;
using Augram.App.Overlay;
using Augram.App.Sync;
using Augram.App.Training;
using Augram.App.Transfer;
using Augram.App.UsedBy;
using Augram.App.ViewModels;
using Augram.App.Views;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Augram.Core.Steps;
using Augram.Core.Training;
using Augram.Core.Transfer;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Architecture.SourceScan;
using static Augram.App.Tests.Transfer.TransferTestData;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// Every dialog on one base (plan 0007 S19; docs/reference/one-home-guards.md): <see cref="DialogWindow"/> is the window,
/// <see cref="DialogShell"/> the chrome, <see cref="DialogHost"/> the one that shows it.
/// </summary>
public sealed partial class OneHomeDialogTests
{
    private const string App = "src/Augram.App/";

    private static IEnumerable<string> AppCode => Code("src/Augram.App", "tools");

    /// <summary>One way to build each dialog, for the headless check that it is on the shell. A new dialog adds its line here.</summary>
    private static readonly Dictionary<Type, Func<DialogWindow>> Dialogs = new()
    {
        [typeof(ConfirmDialog)] = () => new ConfirmDialog("Delete gesture", "Delete 'Down'?", "Delete"),
        [typeof(FormDialogWindow)] = () => new FormDialogWindow(new FormDialog(), "New app group"),
        [typeof(UsedByWindow)] = () => new UsedByWindow(new GestureId(Guid.NewGuid()), "Down", new MappingStore(), null),
        [typeof(SyncConflictWindow)] = () => new SyncConflictWindow(new SyncConflictsViewModel([], [], MappingDocument.Empty)),
        [typeof(AugramImportWindow)] = AugramImport,
        [typeof(GesturePickerWindow)] = () => new GesturePickerWindow(new GesturePicker { Tiles = [.. StarterGestures.All().Select(GestureTileItem.From)] }),
        [typeof(TrainingWindow)] = Training,
        [typeof(ImportDialog)] = () => new ImportDialog(new ImportViewModel(new GestureLibrary(), new MappingStore(), NullEventLog.Instance)),
    };

    [Fact]
    public void OnlyTheThreeBaseWindowsDeriveFromWindow()
    {
        Type[] bases = [typeof(MainWindow), typeof(TrailOverlayWindow), typeof(DialogWindow)];
        var offenders = typeof(DialogWindow).Assembly.GetTypes()
            .Where(type => typeof(Window).IsAssignableFrom(type) && type != typeof(Window))
            .Where(type => !bases.Contains(type) && !type.IsSubclassOf(typeof(DialogWindow)))
            .Select(type => $"{type.FullName} : {type.BaseType?.Name}")
            .ToList();

        Assert.True(offenders.Count == 0, "Windows that are not a DialogWindow: " + string.Join(", ", offenders) + ". Derive a dialog from Dialogs/DialogWindow and put its content in a DialogShell (plan 0007 S19); only MainWindow, TrailOverlayWindow and DialogWindow derive from Window.");
        Assert.All(bases, type => Assert.Equal(typeof(Window), type.BaseType));
    }

    [Fact]
    public void NoWindowIsBuiltOutsideTheBases()
    {
        AssertOnlyIn(
            NewWindow(),
            AppCode,
            "Derive a dialog from Dialogs/DialogWindow instead of building a Window (plan 0007 S19).");
        AssertOnlyIn(
            WindowRoot(),
            Markup("src/Augram.App"),
            "A dialog is a code-only Dialogs/DialogWindow with its content in a DialogShell; only Views/MainWindow.axaml is a Window in XAML (plan 0007 S19).",
            new Allowed(App + "Views/MainWindow.axaml", "the main window"));
    }

    [Fact]
    public void DialogsAreShownAndPlacedOnlyByTheirBase()
    {
        AssertOnlyIn(
            ShowDialog(),
            AppCode,
            "Show a dialog with Dialogs/DialogHost (ShowModalAsync, Show, ShowAloneAsync) (plan 0007 S19).",
            new Allowed(App + "Dialogs/DialogHost.cs", "the home"),
            new Allowed(App + "Dialogs/ConfirmPresenter.cs", "reads the confirmation's answer from the modal result"));
        AssertOnlyIn(
            DialogSizeApply(),
            AppCode,
            "Pass the size to the Dialogs/DialogWindow base constructor (plan 0007 S19).",
            new Allowed(App + "Dialogs/DialogWindow.cs", "the home"));
        AssertOnlyIn(
            StartupLocation(),
            AppCode,
            "Dialogs/DialogWindow centres a dialog on its owner and DialogHost.ShowAloneAsync on the screen (plan 0007 S19).",
            new Allowed(App + "Dialogs/", "the home"),
            new Allowed(App + "Overlay/TrailOverlayWindow.cs", "the overlay places itself over the screens (Manual)"));
    }

    [Fact]
    public void DialogButtonsAreTheShells()
        => AssertOnlyIn(
            DialogButtons(),
            Code("src/Augram.App").Concat(Markup("src/Augram.App")),
            "Put a dialog's content in Dialogs/DialogShell, which lays out its buttons (ConfirmLabel, CancelLabel, LeadingButtons) (plan 0007 S19).",
            new Allowed(App + "Themes/Default/Lists.axaml", "the DialogShell's theme"));

    [AvaloniaFact]
    public void EveryDialogIsOnTheShell()
    {
        var dialogs = typeof(DialogWindow).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(DialogWindow)) && !type.IsAbstract).ToHashSet();
        Assert.True(dialogs.SetEquals(Dialogs.Keys), "Dialogs without a line in OneHomeDialogTests.Dialogs: " + string.Join(", ", dialogs.Except(Dialogs.Keys).Select(type => type.Name)));

        foreach (var (type, create) in Dialogs)
        {
            var window = create();
            window.Show();
            Assert.True(window.GetVisualDescendants().OfType<DialogShell>().Any(), $"{type.Name} shows no DialogShell: put its content in Dialogs/DialogShell (plan 0007 S19).");
            window.Close();
        }
    }

    private static AugramImportWindow AugramImport()
    {
        var mine = Configuration();
        var config = Session(mine);
        var file = TransferSerializer.Read(TransferSerializer.Write(Exporter.Export(ExportScope.GesturesOnly, mine)), StepRegistry.BuiltIn);
        var window = new AugramImportWindow(new AugramImportViewModel(config, file, "gestures.augram.json", new ListEventLog()));
        window.Closed += (_, _) => config.Dispose();
        return window;
    }

    private static TrainingWindow Training()
    {
        var session = new TrainingSession(new GestureLibrary(), () => RecognitionOptions.Default);
        session.Begin(TrainingRequest.NewGesture);
        return new TrainingWindow(new TrainingViewModel(session));
    }

    [GeneratedRegex(@"\bnew\s+Window\s*[({]")]
    private static partial Regex NewWindow();

    [GeneratedRegex(@"<Window\b")]
    private static partial Regex WindowRoot();

    [GeneratedRegex(@"\.ShowDialog\s*(?:<[^>]*>)?\(")]
    private static partial Regex ShowDialog();

    [GeneratedRegex(@"\bDialogSize\.Apply\(")]
    private static partial Regex DialogSizeApply();

    [GeneratedRegex(@"\bWindowStartupLocation\b")]
    private static partial Regex StartupLocation();

    [GeneratedRegex(@"\bdialog-buttons\b|\bDockPanel\.dialog\b")]
    private static partial Regex DialogButtons();
}
