using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Augram.App.Import;

/// <summary>
/// The import flow as the user sees it: a file picker starting at SP.net's live config folder when
/// this machine has one, then <see cref="ImportDialog"/> over an <see cref="ImportViewModel"/>, modal
/// to the main window. Nothing here decides what gets merged.
/// </summary>
public sealed class ImportPresenter : IImportPresenter
{
    private readonly GestureLibrary _library;
    private readonly IEventLog _log;

    public ImportPresenter(GestureLibrary library, IEventLog log)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(log);
        _library = library;
        _log = log;
    }

    public async Task OpenAsync()
    {
        if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } owner)
        {
            return;
        }

        var path = await PickFileAsync(owner).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        var viewModel = new ImportViewModel(_library, _log);
        viewModel.Load(path);
        await new ImportDialog(viewModel).ShowDialog(owner).ConfigureAwait(true);
    }

    private static async Task<string?> PickFileAsync(Window owner)
    {
        var storage = owner.StorageProvider;
        var defaultPath = ImportViewModel.DefaultSourcePath;
        var options = new FilePickerOpenOptions
        {
            Title = "Import gestures from StrokesPlus.net",
            AllowMultiple = false,
            SuggestedFileName = ImportViewModel.SuggestedFileName,
            FileTypeFilter = [new FilePickerFileType("StrokesPlus.net config") { Patterns = ["*.json"] }, FilePickerFileTypes.All],
        };
        if (defaultPath is not null && Path.GetDirectoryName(defaultPath) is { } folder)
        {
            options.SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(folder).ConfigureAwait(true);
        }

        var files = await storage.OpenFilePickerAsync(options).ConfigureAwait(true);
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
