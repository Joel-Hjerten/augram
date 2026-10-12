using Augram.App.Dialogs;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Import.StrokesPlus;
using Avalonia.Platform.Storage;

namespace Augram.App.Import;

/// <summary>
/// The import flow as the user sees it: the shared open picker (<see cref="ITransferFilePicker"/>, asked with
/// <see cref="OpenRequest"/>: it starts at SP.net's live config folder when this machine has one), the file read through
/// <see cref="StrokesPlusImporter.ReadAll(Stream)"/> (<see cref="Read"/>), then <see cref="ImportDialog"/> over an
/// <see cref="ImportViewModel"/>, modal to the main window (<see cref="DialogHost.ShowModalAsync"/>). Nothing here decides what gets merged.
/// </summary>
public sealed class ImportPresenter : IImportPresenter
{
    /// <summary>The open dialog's, the import dialog's and its heading's title.</summary>
    public const string Title = "Import from StrokesPlus.net";
    private const string SourceFileName = "StrokesPlus.net.json";

    private static readonly FilePickerFileType StrokesPlusFiles = new("StrokesPlus.net config") { Patterns = ["*.json"] };

    private readonly GestureLibrary _library;
    private readonly MappingStore _mapping;
    private readonly ITransferFilePicker _picker;
    private readonly IEventLog _log;

    public ImportPresenter(GestureLibrary library, MappingStore mapping, ITransferFilePicker picker, IEventLog log)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(picker);
        ArgumentNullException.ThrowIfNull(log);
        _library = library;
        _mapping = mapping;
        _picker = picker;
        _log = log;
    }

    /// <summary>
    /// The open dialog: "StrokesPlus.net config (*.json)" and all files, suggesting <c>StrokesPlus.net.json</c>, starting in
    /// SP.net's own folder when its live config exists on this machine.
    /// </summary>
    public static FileOpenRequest OpenRequest => new(Title, [StrokesPlusFiles, FilePickerFileTypes.All])
    {
        SuggestedFileName = SourceFileName,
        StartFolder = DefaultSourceFolder,
    };

    /// <summary>The folder of SP.net's live config, when this machine has one.</summary>
    private static string? DefaultSourceFolder
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (appData.Length == 0)
            {
                return null;
            }

            var path = Path.Combine(appData, "StrokesPlus.net", SourceFileName);
            return File.Exists(path) ? Path.GetDirectoryName(path) : null;
        }
    }

    public async Task OpenAsync()
    {
        if (DialogHost.MainWindow is null)
        {
            return;
        }

        var path = await _picker.PickOpenAsync(OpenRequest).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        var viewModel = new ImportViewModel(_library, _mapping, _log);
        Read(viewModel, path);
        await DialogHost.ShowModalAsync(new ImportDialog(viewModel), activateWhenAlone: true).ConfigureAwait(true);
    }

    /// <summary>Reads the file at <paramref name="path"/> into <paramref name="viewModel"/>; a file that cannot be read or is not SP.net JSON becomes its <see cref="ImportViewModel.Error"/>.</summary>
    public static void Read(ImportViewModel viewModel, string path)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var stream = File.OpenRead(path);
            viewModel.Load(path, StrokesPlusImporter.ReadAll(stream));
        }
        catch (Exception exception) when (exception is ImportFormatException or IOException or UnauthorizedAccessException)
        {
            viewModel.LoadFailed(path, exception.Message);
        }
    }
}
