namespace Augram.App.Transfer;

/// <summary>
/// The file pickers of export and import (plan 0003): the save picker for an Augram file and the open picker for any Augram
/// JSON. Null is Cancel. <see cref="TransferFilePicker"/> is the platform's dialogs; tests substitute a fake that answers a
/// temp path, so no test ever opens a real picker.
/// </summary>
public interface ITransferFilePicker
{
    /// <summary>Where to write the export, starting at <paramref name="suggestedFileName"/>; null when cancelled.</summary>
    Task<string?> PickSaveAsync(string suggestedFileName);

    /// <summary>The Augram file (or any JSON) to import; null when cancelled.</summary>
    Task<string?> PickOpenAsync();
}
