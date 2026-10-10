using Augram.App.Declarations;
using Augram.App.Tests.Commands;
using Augram.App.Tests.Support;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;
using Augram.Core.Transfer;
using Xunit;
using static Augram.App.Tests.Transfer.TransferTestData;

namespace Augram.App.Tests.Transfer;

/// <summary>
/// The export flow behind its interface (plan 0003 step 3), with the dialog and the picker faked: the dialog starts on the
/// preselection and can change it, the picker gets the suggested name, the file is written through a temp file and reads
/// back, the log has counts only; Cancel at either step writes nothing; a failed write says why.
/// </summary>
public sealed class ExportPresenterTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 10);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "augram-export-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeFormDialogPresenter _dialogs = new();
    private readonly FakeTransferFilePicker _picker = new();
    private readonly ListEventLog _log = new();

    public ExportPresenterTests()
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
    public async Task SaveWritesThePreselectedGroupToThePickedPath_AndLogsCountsOnly()
    {
        using var session = Session(Configuration());
        var blender = GroupNamed(session.Mapping.Current, "Blender");
        _picker.SavePath = Path.Combine(_folder, "Augram Blender 2026-10-10.augram.json");

        var outcome = await Presenter(session).ExportAsync(ExportScope.Of([blender.Id]));

        var request = _dialogs.Last;
        Assert.Equal((ExportViewModel.Title, ExportViewModel.ConfirmLabel), (request.Title, request.ConfirmLabel));
        Assert.True(request.CanConfirm!.Get());
        Assert.Equal(["Augram Blender 2026-10-10.augram.json"], _picker.SuggestedNames);
        Assert.Equal("Exported 1 gesture, 1 app group, 1 hold remap, 2 commands to Augram Blender 2026-10-10.augram.json.", outcome);
        var file = TransferSerializer.Read(File.ReadAllText(_picker.SavePath), StepRegistry.BuiltIn);
        Assert.Equal(["Global", "Blender"], file.Mapping!.Groups.Select(group => group.Name));
        Assert.Equal(blender.Id, file.Mapping.Groups[1].Id);
        Assert.Null(file.Settings);
        Assert.Equal([_picker.SavePath], Directory.GetFiles(_folder));

        var logged = Assert.Single(_log.Events, e => e.Source == ExportPresenter.LogSource);
        Assert.Equal((EventLevel.Info, ExportPresenter.LogMessage), (logged.Level, logged.Message));
        Assert.DoesNotContain(logged.Properties!, property => property.Value?.ToString()?.Contains("Blender", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task TheDialogCanChangeTheScope_ThroughItsDeclaredForm()
    {
        using var session = Session(Configuration());
        _picker.SavePath = Path.Combine(_folder, "gestures.augram.json");
        _dialogs.Answer = request =>
        {
            var scope = request.Screen!.Sections[0].Fields.OfType<ButtonRadioField<ExportKind>>().Single();
            scope.Value.Set(ExportKind.GesturesOnly);
            return true;
        };

        await Presenter(session).ExportAsync(ExportScope.Everything);

        Assert.Equal(["Augram gestures 2026-10-10.augram.json"], _picker.SuggestedNames);
        var file = TransferSerializer.Read(File.ReadAllText(_picker.SavePath), StepRegistry.BuiltIn);
        Assert.Null(file.Mapping);
        Assert.Equal(session.Gestures.All.Count, file.Gestures.Count);
    }

    [Fact]
    public async Task CancelInTheDialogOrThePicker_WritesNothing()
    {
        using var session = Session(Configuration());
        _picker.SavePath = Path.Combine(_folder, "never.augram.json");
        _dialogs.Answer = _ => false;

        Assert.Null(await Presenter(session).ExportAsync(ExportScope.Everything));
        Assert.Empty(_picker.SuggestedNames);

        _dialogs.Answer = _ => true;
        _picker.SavePath = null;
        Assert.Null(await Presenter(session).ExportAsync(ExportScope.Everything));
        Assert.Single(_picker.SuggestedNames);
        Assert.Empty(Directory.GetFiles(_folder));
        Assert.DoesNotContain(_log.Events, e => e.Source == ExportPresenter.LogSource);
    }

    [Fact]
    public async Task SaveIsDisabledWhileNothingIsSelected()
    {
        using var session = Session(Configuration());

        Assert.Null(await Presenter(session).ExportAsync(ExportScope.Of([])));

        Assert.False(_dialogs.Last.CanConfirm!.Get());
        Assert.Empty(_picker.SuggestedNames);
    }

    [Fact]
    public async Task AFailedWriteSaysWhy_AndLeavesNoFile()
    {
        using var session = Session(Configuration());
        _picker.SavePath = Path.Combine(_folder, "missing folder", "x.augram.json");

        var outcome = await Presenter(session).ExportAsync(ExportScope.Everything);

        Assert.StartsWith("Could not save x.augram.json: ", outcome, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_folder, "*", SearchOption.AllDirectories));
        Assert.Contains(_log.Events, e => e.Source == ExportPresenter.LogSource && e.Level == EventLevel.Warning);
    }

    [Fact]
    public void ASavePathTypedWithoutJsonGetsTheAugramExtension()
    {
        Assert.Equal("Blender.augram.json", TransferFilePicker.WithExtension("Blender"));
        Assert.Equal("Blender.json", TransferFilePicker.WithExtension("Blender.json"));
        Assert.Equal("Blender.augram.json", TransferFilePicker.WithExtension("Blender.augram.json"));
    }

    private ExportPresenter Presenter(Core.Config.ConfigSession session) => new(session, _dialogs, _picker, _log, () => Today);
}
