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
/// run was in. A save path typed without <c>.json</c> gets <see cref="TransferFile.Extension"/>. Without a main window there
/// is nothing to show a dialog over, and the answer is Cancel.
/// </summary>
public sealed class TransferFilePicker : ITransferFilePicker
{
    public const string SaveTitle = "Export Augram file";
    public const string OpenTitle = "Import Augram file";

    private string? _lastFolder;

    public static FilePickerFileType AugramFiles { get; } = new("Augram files (*" + TransferFile.Extension + ")") { Patterns = ["*" + TransferFile.Extension] };

    public static FilePickerFileType JsonFiles { get; } = new("JSON (*.json)") { Patterns = ["*.json"] };

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
            SuggestedStartLocation = await StartAsync(owner.StorageProvider).ConfigureAwait(true),
        };
        var file = await owner.StorageProvider.SaveFilePickerAsync(options).ConfigureAwait(true);
        return Remember(file?.TryGetLocalPath()) is { } path ? WithExtension(path) : null;
    }

    public async Task<string?> PickOpenAsync()
    {
        if (Owner() is not { } owner)
        {
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = OpenTitle,
            AllowMultiple = false,
            FileTypeFilter = [AugramFiles, JsonFiles, FilePickerFileTypes.All],
            SuggestedStartLocation = await StartAsync(owner.StorageProvider).ConfigureAwait(true),
        };
        var files = await owner.StorageProvider.OpenFilePickerAsync(options).ConfigureAwait(true);
        return Remember(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }

    /// <summary>"Blender" → "Blender.augram.json"; a name that already ends in <c>.json</c> is kept as typed.</summary>
    public static string WithExtension(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path : path + TransferFile.Extension;
    }

    private static Window? Owner() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    private async Task<IStorageFolder?> StartAsync(IStorageProvider storage)
        => _lastFolder is { } folder ? await storage.TryGetFolderFromPathAsync(folder).ConfigureAwait(true) : null;

    private string? Remember(string? path)
    {
        if (path is not null && Path.GetDirectoryName(path) is { Length: > 0 } folder)
        {
            _lastFolder = folder;
        }

        return path;
    }
}
