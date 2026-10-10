using Augram.App.Tests.Support;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Diagnostics;
using Augram.Core.Transfer;
using Xunit;
using static Augram.App.Tests.Transfer.TransferTestData;

namespace Augram.App.Tests.Transfer;

/// <summary>
/// The import flow behind its interface (plan 0003 step 4), with the picker and the windows faked: the review over the picked
/// file and its summary afterwards; Cancel at the picker or in the review changes nothing; a file that cannot be read, or
/// was written by a newer Augram, is said in a message and imports nothing.
/// </summary>
public sealed class AugramImportPresenterTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "augram-import-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTransferFilePicker _picker = new();
    private readonly FakeAugramImportWindows _windows = new();
    private readonly ListEventLog _log = new();

    public AugramImportPresenterTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is harmless.
        }
    }

    [Fact]
    public async Task ThePickedFileIsReviewed_Imported_AndSummarised()
    {
        var source = Configuration();
        using var session = Session(WithoutGroup(Configuration(), "Blender"));
        _picker.OpenPath = Write("Blender.augram.json", TransferSerializer.Write(Exporter.Export(ExportScope.Of([GroupNamed(source.Mapping, "Blender").Id]), source)));

        var outcome = await Presenter(session).OpenAsync();

        Assert.Equal("Imported from Blender.augram.json: 1 app group, 1 hold remap and 2 commands added.", outcome);
        Assert.Equal("Blender.augram.json", Assert.Single(_windows.Reviews).FileName);
        Assert.Equal((AugramImportViewModel.Title, outcome!), Assert.Single(_windows.Messages));
        Assert.Contains(session.Mapping.Current.Groups, group => group.Name == "Blender");
    }

    [Fact]
    public async Task TheSummaryListsTheRepairsTheImportNeeded()
    {
        var mine = Configuration();
        var theirs = WithGroup(mine, "Chrome", group => group with
        {
            Commands =
            [
                .. group.Commands.Select(command => command.Name == "Close tab" ? command with { Trigger = Core.Mapping.Trigger.ForGesture(Down) } : command),
                Cmd("Back", Core.Mapping.Trigger.ForGesture(Up)),
            ],
        });
        using var session = Session(mine);
        _picker.OpenPath = Write("Chrome.augram.json", TransferSerializer.Write(Exporter.Export(ExportScope.Everything, theirs)));

        await Presenter(session).OpenAsync();

        var message = Assert.Single(_windows.Messages).Message;
        Assert.StartsWith("Imported from Chrome.augram.json: 1 command added.", message, StringComparison.Ordinal);
        Assert.Contains("'Back'", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelAtThePickerOrInTheReview_ChangesNothing()
    {
        var source = Configuration();
        using var session = Session(WithoutGroup(Configuration(), "Blender"));

        Assert.Null(await Presenter(session).OpenAsync());
        Assert.Empty(_windows.Reviews);

        _picker.OpenPath = Write("Blender.augram.json", TransferSerializer.Write(Exporter.Export(ExportScope.Of([GroupNamed(source.Mapping, "Blender").Id]), source)));
        _windows.OnReview = review => review.Cancel();
        Assert.Null(await Presenter(session).OpenAsync());
        Assert.Single(_windows.Reviews);
        Assert.Empty(_windows.Messages);
        Assert.False(session.Mapping.CanUndo);
    }

    [Fact]
    public async Task AFileWrittenByANewerAugram_IsRefusedWithItsReason()
    {
        using var session = Session(Configuration());
        _picker.OpenPath = Write("future.augram.json", """{ "schemaVersion": 999, "gestures": [] }""");

        var outcome = await Presenter(session).OpenAsync();

        Assert.StartsWith("future.augram.json was not imported. The file was written by a newer Augram", outcome, StringComparison.Ordinal);
        Assert.Equal(outcome, Assert.Single(_windows.Messages).Message);
        Assert.Empty(_windows.Reviews);
        Assert.Contains(_log.Events, e => e.Source == AugramImportViewModel.LogSource && e.Level == EventLevel.Warning);
    }

    [Fact]
    public async Task AFileThatCannotBeReadOrIsNotAugramJson_IsSaidInAMessage()
    {
        using var session = Session(Configuration());
        _picker.OpenPath = Path.Combine(_folder, "missing.augram.json");

        Assert.StartsWith("missing.augram.json was not imported. ", await Presenter(session).OpenAsync(), StringComparison.Ordinal);

        _picker.OpenPath = Write("notes.json", "not json at all");
        Assert.StartsWith("notes.json was not imported. ", await Presenter(session).OpenAsync(), StringComparison.Ordinal);
        Assert.Equal(2, _windows.Messages.Count);
        Assert.Empty(_windows.Reviews);
    }

    private AugramImportPresenter Presenter(Core.Config.ConfigSession session) => new(session, _picker, _windows, _log);

    private string Write(string name, string json)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, json);
        return path;
    }
}
