using Augram.Core.Transfer;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Augram.App.Transfer;

/// <summary>
/// The platform's save and open dialogs over the main window's storage provider (plan 0003, decision 3): save offers
/// "Augram files (*.augram.json)" with the export's suggested name; open offers that and "JSON (*.json)", so
/// <c>augram.json</c>, a backup or a sync machine file can be picked too. Each starts in the folder the last pick of this
/// run was in. A save path typed without <c>.json</c> gets <see cref="TransferFile.Extension"/>. The open dialog also serves
/// the StrokesPlus.net import, whose <see cref="FileOpenRequest"/> brings its own title, filters, file name and start folder
/// and leaves the remembered folder alone. Without a main window there is nothing to show a dialog over, and the answer is
/// Cancel.
/// </summary>
public sealed class TransferFilePicker : ITransferFilePicker
{
    public const string SaveTitle = "Export Augram file";
    public const string OpenTitle = "Import Augram file";

    private string? _lastFolder;

    public static FilePickerFileType AugramFiles { get; } = new("Augram files (*" + TransferFile.Extension + ")") { Patterns = ["*" + TransferFile.Extension] };

    public static FilePickerFileType JsonFiles { get; } = new("JSON (*.json)") { Patterns = ["*.json"] };

    /// <summary>The Augram import's open dialog: Augram files, JSON, all files; starts in the last folder of this run.</summary>
    public static FileOpenRequest OpenAugramFile { get; } = new(OpenTitle, [AugramFiles, JsonFiles, FilePickerFileTypes.All]) { FollowsLastFolder = true };

    public async Task<string?> PickSaveAsync(string suggestedFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        if (Owner() is not { } owner)
        {
            return null;
        }

        var options = new FilePickerSaveOptions
        {
            Title = SaveTitle,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = [AugramFiles],
            ShowOverwritePrompt = true,
            SuggestedStartLocation = await StartAsync(owner.StorageProvider, _lastFolder).ConfigureAwait(true),
        };
        var file = await owner.StorageProvider.SaveFilePickerAsync(options).ConfigureAwait(true);
        return Remember(file?.TryGetLocalPath()) is { } path ? WithExtension(path) : null;
    }

    public async Task<string?> PickOpenAsync(FileOpenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Owner() is not { } owner)
        {
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = request.Title,
            AllowMultiple = false,
            SuggestedFileName = request.SuggestedFileName,
            FileTypeFilter = request.Filters,
            SuggestedStartLocation = await StartAsync(owner.StorageProvider, request.FollowsLastFolder ? _lastFolder : request.StartFolder).ConfigureAwait(true),
        };
        var files = await owner.StorageProvider.OpenFilePickerAsync(options).ConfigureAwait(true);
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        return request.FollowsLastFolder ? Remember(path) : path;
    }

    /// <summary>"Blender" → "Blender.augram.json"; a name that already ends in <c>.json</c> is kept as typed.</summary>
    public static string WithExtension(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path : path + TransferFile.Extension;
    }

    private static Window? Owner() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private static async Task<IStorageFolder?> StartAsync(IStorageProvider storage, string? folder)
        => folder is not null ? await storage.TryGetFolderFromPathAsync(folder).ConfigureAwait(true) : null;

    private string? Remember(string? path)
    {
        if (path is not null && Path.GetDirectoryName(path) is { Length: > 0 } folder)
        {
            _lastFolder = folder;
        }

        return path;
    }
}
