namespace Augram.App.Transfer;

/// <summary>
/// The file pickers of export and import (plan 0003): the save picker for an Augram file and one open picker for every
/// import, the Augram file (<see cref="TransferFilePicker.OpenAugramFile"/>) and the StrokesPlus.net config
/// (<see cref="Import.ImportPresenter.OpenRequest"/>). Null is Cancel. <see cref="TransferFilePicker"/> is the platform's
/// dialogs; tests substitute a fake that answers a temp path, so no test ever opens a real picker.
/// </summary>
public interface ITransferFilePicker
{
    /// <summary>Where to write the export, starting at <paramref name="suggestedFileName"/>; null when cancelled.</summary>
    Task<string?> PickSaveAsync(string suggestedFileName);

    /// <summary>The file to import, picked as <paramref name="request"/> says; null when cancelled.</summary>
    Task<string?> PickOpenAsync(FileOpenRequest request);
}
